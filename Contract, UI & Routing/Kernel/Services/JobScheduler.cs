﻿using SoftwareCenter.Core.Jobs;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// A standard, in-memory implementation of the IJobScheduler interface for managing background jobs.
    /// This implementation uses `System.Threading.Timer` for scheduling.
    /// </summary>
    public class JobScheduler : IJobScheduler
    {
        private readonly ConcurrentDictionary<string, Timer> _timers = new();
        private readonly ConcurrentDictionary<string, Func<Task>> _actions = new();

        /// <inheritdoc />
        public void ScheduleRecurring(string jobName, TimeSpan interval, Func<Task> action)
        {
            if (string.IsNullOrWhiteSpace(jobName)) throw new ArgumentNullException(nameof(jobName));
            if (action == null) throw new ArgumentNullException(nameof(action));

            // Store the action
            _actions[jobName] = action;

            // Create a timer that will execute the job.
            // The `_` discards the state object, which we don't need here.
            var timer = new Timer(async _ =>
            {
                if (_actions.TryGetValue(jobName, out var jobAction))
                {
                    // Fire-and-forget the task to prevent the timer callback from blocking.
                    // In a real-world scenario, you'd add robust error handling here.
                    _ = jobAction();
                }
            }, null, TimeSpan.Zero, interval);

            // If there's an old timer, dispose of it before storing the new one.
            if (_timers.TryRemove(jobName, out var oldTimer))
            {
                oldTimer.Dispose();
            }

            _timers[jobName] = timer;
        }

        /// <inheritdoc />
        public void Unschedule(string jobName)
        {
            if (_timers.TryRemove(jobName, out var timer))
            {
                timer.Dispose();
            }
            _actions.TryRemove(jobName, out _);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var timer in _timers.Values)
            {
                timer.Dispose();
            }
            _timers.Clear();
            _actions.Clear();
        }
    }
}