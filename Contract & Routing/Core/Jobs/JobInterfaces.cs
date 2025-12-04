using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SoftwareCenter.Core.Diagnostics;

namespace SoftwareCenter.Core.Jobs
{
    /// <summary>
    /// Represents a recurring background task provided by a Module.
    /// </summary>
    public interface IJob
    {
        /// <summary>
        /// Unique identifier (e.g., "BackupModule.DailyBackup").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// How often the job should run.
        /// </summary>
        TimeSpan Interval { get; }

        /// <summary>
        /// The logic to execute.
        /// </summary>
        Task ExecuteAsync(JobContext context);
    }

    /// <summary>
    /// Context passed to a running job.
    /// Provides traceability and cancellation support.
    /// </summary>
    public class JobContext
    {
        public TraceContext Trace { get; set; } = new TraceContext();
        public DateTime LastRun { get; set; }
        public CancellationToken CancellationToken { get; set; }

        // Pass data between runs if needed
        public Dictionary<string, object> State { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Contract for the Centralized Scheduler.
    /// Allows modules to register, pause, or manually trigger jobs.
    /// </summary>
    public interface IJobScheduler
    {
        void Register(IJob job);

        /// <summary>
        /// Manually runs a job immediately (e.g., User clicked "Run Now").
        /// </summary>
        void TriggerAsync(string jobName);

        void Pause(string jobName);
        void Resume(string jobName);
    }
}