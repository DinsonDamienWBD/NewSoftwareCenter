using System;
using SoftwareCenter.Core.Jobs;

namespace SoftwareCenter.Kernel.Contracts
{
    /// <summary>
    /// Contract for the Centralized Scheduler.
    /// Allows modules to register, pause, or manually trigger jobs.
    /// </summary>
    public interface IJobScheduler : IDisposable
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