using System;
using System.Threading.Tasks;

namespace SoftwareCenter.Core.Jobs
{
    /// <summary>
    /// Defines the contract for a service that can schedule and manage background jobs.
    /// </summary>
    public interface IJobScheduler : IDisposable
    {
        /// <summary>
        /// Schedules a recurring job.
        /// </summary>
        /// <param name="jobName">A unique name for the job.</param>
        /// <param name="interval">The time interval between executions.</param>
        /// <param name="action">The action to execute.</param>
        void ScheduleRecurring(string jobName, TimeSpan interval, Func<Task> action);

        /// <summary>
        /// Removes a scheduled job.
        /// </summary>
        /// <param name="jobName">The name of the job to remove.</param>
        void Unschedule(string jobName);
    }
}