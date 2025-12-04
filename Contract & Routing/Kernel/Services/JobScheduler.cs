using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.Core.Jobs;

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// Implements Option B: Centralized Job Scheduling.
    /// Manages lifecycles, crashes, and logging for background tasks.
    /// </summary>
    public class JobScheduler : IJobScheduler, IDisposable
    {
        private readonly KernelLogger _logger;
        private readonly ConcurrentDictionary<string, Timer> _timers = new();
        private readonly ConcurrentDictionary<string, IJob> _jobs = new();
        private readonly CancellationTokenSource _shutdownToken = new();

        public JobScheduler(KernelLogger logger)
        {
            _logger = logger;
        }

        public void Register(IJob job)
        {
            if (_jobs.TryAdd(job.Name, job))
            {
                // Schedule the timer
                // We use System.Threading.Timer which is efficient for background tasks
                var timer = new Timer(async _ => await RunJobSafe(job), null, job.Interval, job.Interval);
                _timers.TryAdd(job.Name, timer);

                // Log registration
                _logger.LogExecutionAsync(new JobCommandStub(job.Name), true, 0).Wait();
            }
        }

        public void TriggerAsync(string jobName)
        {
            if (_jobs.TryGetValue(jobName, out var job))
            {
                // Run immediately on thread pool
                Task.Run(() => RunJobSafe(job));
            }
        }

        public void Pause(string jobName)
        {
            if (_timers.TryGetValue(jobName, out var timer))
            {
                timer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        public void Resume(string jobName)
        {
            if (_jobs.TryGetValue(jobName, out var job) && _timers.TryGetValue(jobName, out var timer))
            {
                timer.Change(job.Interval, job.Interval);
            }
        }

        private async Task RunJobSafe(IJob job)
        {
            if (_shutdownToken.IsCancellationRequested) return;

            // Start a new Trace for this job run
            TraceContext.StartNew();

            // Log that we are starting (Visible in Verbose logs)
            // Ideally we'd log "Job Started", but for brevity we rely on the final log.

            var ctx = new JobContext
            {
                Trace = new TraceContext(), // Context for the job logic
                LastRun = DateTime.UtcNow,
                CancellationToken = _shutdownToken.Token
            };

            // Record trace hop
            // Note: Since TraceContext is AsyncLocal, we can also use TraceContext.CurrentTraceId

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            bool success = false;
            string? error = null;

            try
            {
                await job.ExecuteAsync(ctx);
                success = true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            finally
            {
                stopwatch.Stop();
                // Centralized Logging for ALL background tasks
                // This ensures "Job Failures" are events the AI Agent can listen to.
                await _logger.LogExecutionAsync(new JobCommandStub($"Job.{job.Name}"), success, stopwatch.ElapsedMilliseconds, error);
            }
        }

        public void Dispose()
        {
            _shutdownToken.Cancel();
            foreach (var timer in _timers.Values) timer.Dispose();
            _timers.Clear();
        }

        // Helper stub to adapt Job to Logger's ICommand expectation
        // Matches the Core ICommand interface
        private class JobCommandStub : ICommand
        {
            public string Name { get; }
            public Dictionary<string, object> Parameters { get; } = new();
            public Guid TraceId { get; }
            public List<TraceHop> History { get; } = new();

            public JobCommandStub(string name)
            {
                Name = name;
                TraceId = TraceContext.CurrentTraceId ?? Guid.NewGuid();
                History.Add(new TraceHop("JobScheduler", "Executed"));
            }
        }
    }
}