using Microsoft.Extensions.Logging;
using SoftwareCenter.Core.Jobs;
using SoftwareCenter.Kernel.Contracts;
using System;

namespace SoftwareCenter.Kernel.Services
{
    public class JobScheduler : IJobScheduler
    {
        private readonly ILogger<JobScheduler> _logger;

        public JobScheduler(ILogger<JobScheduler> logger)
        {
            _logger = logger;
        }

        public void Dispose()
        {
            
        }

        public void Pause(string jobName)
        {
            _logger.LogInformation($"Pausing job {jobName}");
        }

        public void Register(IJob job)
        {
            _logger.LogInformation($"Registering job {job.Name}");
        }

        public void Resume(string jobName)
        {
            _logger.LogInformation($"Resuming job {jobName}");
        }

        public void TriggerAsync(string jobName)
        {
            _logger.LogInformation($"Triggering job {jobName}");
        }
    }
}