using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.Core.Jobs;
using SoftwareCenter.Core.Errors;

namespace SoftwareCenter.Kernel.Services
﻿{
﻿    public class JobSchedulerService : BackgroundService
﻿    {
﻿        private readonly IServiceProvider _serviceProvider;
﻿        private readonly ModuleLoader _moduleLoader;
﻿        private readonly ILogger<JobSchedulerService> _logger;
﻿        private readonly IErrorHandler _errorHandler; // Injected IErrorHandler
﻿        private readonly List<JobRunner> _activeJobs = new List<JobRunner>();
﻿        private readonly object _lock = new object(); // For thread-safe access to _activeJobs
﻿
﻿        public JobSchedulerService(
﻿            IServiceProvider serviceProvider,
﻿            ModuleLoader moduleLoader,
﻿            ILogger<JobSchedulerService> logger,
﻿            IErrorHandler errorHandler) // Added IErrorHandler
﻿        {
﻿            _serviceProvider = serviceProvider;
﻿            _moduleLoader = moduleLoader;
﻿            _logger = logger;
﻿            _errorHandler = errorHandler; // Assign IErrorHandler
﻿        }
﻿
﻿        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
﻿        {
﻿            // 1. Discovery Phase: Find all types implementing IJob
﻿            DiscoverJobs();
﻿
﻿            _logger.LogInformation($"JobScheduler started. Managing {_activeJobs.Count} jobs.");
﻿
﻿            // 2. Execution Loop
﻿            while (!stoppingToken.IsCancellationRequested)
﻿            {
﻿                var now = DateTimeOffset.UtcNow;
﻿
﻿                List<JobRunner> jobsToRun;
﻿                lock (_lock)
﻿                {
﻿                    jobsToRun = _activeJobs.Where(runner => runner.ShouldRun(now)).ToList();
﻿                }
﻿
﻿                foreach (var runner in jobsToRun)
﻿                {
﻿                    // Fire and forget - don't await the job so we don't block the scheduler loop
﻿                    _ = ExecuteJobAsync(runner, stoppingToken);
﻿                }
﻿
﻿                // Check schedule every minute
﻿                // Align to the start of the next minute for cleaner scheduling
﻿                var delay = TimeSpan.FromMinutes(1) - TimeSpan.FromSeconds(now.Second);
﻿                if (delay.TotalMilliseconds <= 0) delay = TimeSpan.FromSeconds(0); // Ensure non-negative delay
﻿                await Task.Delay(delay, stoppingToken);
﻿            }
﻿        }
﻿
﻿        /// <summary>
﻿        /// Discovers IJob implementations from loaded assemblies and registers them with the scheduler.
﻿        /// </summary>
﻿        private void DiscoverJobs()
﻿        {
﻿            var assemblies = _moduleLoader.GetLoadedAssemblies();
﻿            var jobInterface = typeof(IJob);
﻿
﻿            foreach (var assembly in assemblies)
﻿            {
﻿                try
﻿                {
﻿                    var jobTypes = assembly.GetTypes()
﻿                        .Where(t => jobInterface.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
﻿
﻿                    foreach (var type in jobTypes)
﻿                    {
﻿                        try
﻿                        {
﻿                            // Create a singleton instance of the job definition to access the CronExpression
﻿                            var jobInstance = (IJob)Activator.CreateInstance(type);
﻿                            RegisterJob(jobInstance, type);
﻿                        }
﻿                        catch (Exception ex)
﻿                        {
﻿                            _errorHandler.HandleError(ex, new TraceContext(), $"Failed to instantiate or register job '{type.Name}' during discovery.");
﻿                        }
﻿                    }
﻿                }
﻿                catch (Exception ex)
﻿                {
﻿                    _errorHandler.HandleError(ex, new TraceContext(), $"Error during job discovery in assembly {assembly.FullName}.");
﻿                }
﻿            }
﻿        }
﻿
﻿        /// <summary>
﻿        /// Registers a new job with the scheduler.
﻿        /// </summary>
﻿        /// <param name="jobInstance">The instance of the job to register.</param>
﻿        /// <param name="jobType">The concrete type of the job.</param>
﻿        public void RegisterJob(IJob jobInstance, Type jobType)
﻿        {
﻿            if (jobInstance == null) throw new ArgumentNullException(nameof(jobInstance));
﻿            if (jobType == null) throw new ArgumentNullException(nameof(jobType));
﻿
﻿            lock (_lock)
﻿            {
﻿                if (_activeJobs.Any(jr => jr.JobType == jobType))
﻿                {
﻿                    var message = $"Job '{jobType.Name}' is already registered.";
﻿                    _errorHandler.HandleError(new InvalidOperationException(message), new TraceContext(), message, isCritical: false);
﻿                    return;
﻿                }
﻿                _activeJobs.Add(new JobRunner(jobInstance, jobType));
﻿                _logger.LogInformation($"Job '{jobType.Name}' registered dynamically.");
﻿            }
﻿        }
﻿
﻿        /// <summary>
﻿        /// Deregisters an existing job from the scheduler.
﻿        /// </summary>
﻿        /// <param name="jobType">The concrete type of the job to deregister.</param>
﻿        public void DeregisterJob(Type jobType)
﻿        {
﻿            if (jobType == null) throw new ArgumentNullException(nameof(jobType));
﻿
﻿            lock (_lock)
﻿            {
﻿                var jobToRemove = _activeJobs.FirstOrDefault(jr => jr.JobType == jobType);
﻿                if (jobToRemove != null)
﻿                {
﻿                    _activeJobs.Remove(jobToRemove);
﻿                    _logger.LogInformation($"Job '{jobType.Name}' deregistered dynamically.");
﻿                }
﻿                else
﻿                {
﻿                    var message = $"Attempted to deregister job '{jobType.Name}', but it was not found.";
﻿                    _errorHandler.HandleError(new InvalidOperationException(message), new TraceContext(), message, isCritical: false);
﻿                }
﻿            }
﻿        }
﻿
﻿        private async Task ExecuteJobAsync(JobRunner runner, CancellationToken token)
﻿        {
﻿            runner.MarkRunning();
﻿
﻿                        using (var scope = _serviceProvider.CreateScope())
﻿                        {
﻿                            var traceContext = new TraceContext();
﻿            
﻿                            try
﻿                            {
﻿                                _logger.LogInformation($"[Scheduler] Starting job '{runner.JobType.Name}' (TraceId: {traceContext.TraceId})");
﻿            
﻿                                // Dynamic resolution of IJobHandler<TJob>
﻿                                var handlerType = typeof(IJobHandler<>).MakeGenericType(runner.JobType);
﻿                                var handler = scope.ServiceProvider.GetService(handlerType);
﻿            ﻿                    if (handler != null)
﻿                    {
﻿                        // Invoke ExecuteAsync(TJob job, ITraceContext traceContext)
﻿                        var method = handlerType.GetMethod("ExecuteAsync");
﻿                        if (method != null)
﻿                        {
﻿                            await (Task)method.Invoke(handler, new object[] { runner.JobInstance, traceContext });
﻿                        }
﻿                        else
﻿                        {
﻿                            var message = $"[Scheduler] Method 'ExecuteAsync' not found on handler for job '{runner.JobType.Name}'.";
﻿                             _errorHandler.HandleError(new InvalidOperationException(message), traceContext, message, isCritical: false);
﻿                        }
﻿                    }
﻿                    else
﻿                    {
﻿                        var message = $"[Scheduler] No handler registered for job '{runner.JobType.Name}'.";
﻿                        _errorHandler.HandleError(new InvalidOperationException(message), traceContext, message, isCritical: false);
﻿                    }
﻿                }
﻿                catch (Exception ex)
﻿                {
﻿                    _errorHandler.HandleError(ex, traceContext, $"[Scheduler] Error executing job '{runner.JobType.Name}'.");
﻿                }
﻿                finally
﻿                {
﻿                    runner.MarkComplete();
﻿                }
﻿            }
﻿        }
﻿
﻿        /// <summary>
﻿        /// Internal wrapper to track job state and schedule.
﻿        /// </summary>
﻿        private class JobRunner
﻿        {
﻿            public IJob JobInstance { get; }
﻿            public Type JobType { get; }
﻿            public DateTimeOffset LastRun { get; private set; } = DateTimeOffset.MinValue;
﻿            public bool IsRunning { get; private set; }
﻿
﻿            private readonly SimpleCronParser _cronParser;
﻿
﻿            public JobRunner(IJob job, Type type)
﻿            {
﻿                JobInstance = job;
﻿                JobType = type;
﻿                _cronParser = new SimpleCronParser(job.CronExpression);
﻿            }
﻿
﻿            public bool ShouldRun(DateTimeOffset now)
﻿            {
﻿                if (IsRunning) return false; // Prevent overlapping execution
﻿
﻿                // Check if the current minute matches the Cron expression
﻿                // and we haven't already run in this minute.
﻿                bool matchesSchedule = _cronParser.IsMatch(now);
﻿                bool notRunThisMinute = LastRun.Minute != now.Minute || (now - LastRun).TotalMinutes >= 1;
﻿
﻿                return matchesSchedule && notRunThisMinute;
﻿            }
﻿
﻿            public void MarkRunning() => IsRunning = true;
﻿
﻿            public void MarkComplete()
﻿            {
﻿                IsRunning = false;
﻿                LastRun = DateTimeOffset.UtcNow;
﻿            }
﻿        }
﻿    }
﻿}
﻿