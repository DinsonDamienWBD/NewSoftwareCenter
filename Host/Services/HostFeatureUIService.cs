using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Commands.UI;
using SoftwareCenter.Kernel.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SoftwareCenter.Host.Services
{
    public class HostFeatureUIService : IHostedService
    {
        private readonly ILogger<HostFeatureUIService> _logger;
        private readonly ICommandBus _commandBus;

        public HostFeatureUIService(ILogger<HostFeatureUIService> logger, ICommandBus commandBus)
        {
            _logger = logger;
            _commandBus = commandBus;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Host Feature UI Service is starting.");

            try
            {
                // 1. Create UI for AppManager
                await CreateFeatureUI("AppManager", "appmanager-icon");

                // 2. Create UI for SourceManager
                await CreateFeatureUI("SourceManager", "sourcemanager-icon");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create host feature UI.");
            }
        }

        private async Task CreateFeatureUI(string featureName, string icon)
        {
            // Create Content Container
            var contentContainerId = await _commandBus.Dispatch<string>(new CreateUIElementCommand(
                ownerModuleId: "Host",
                parentId: "content-zone",
                elementType: "content-container",
                initialProperties: new Dictionary<string, object>
                {
                    { "Title", featureName }
                }
            ));
            _logger.LogInformation($"Created content container for {featureName} with ID: {contentContainerId}");

            // Create Navigation Button
            var navButtonId = await _commandBus.Dispatch<string>(new CreateUIElementCommand(
                ownerModuleId: "Host",
                parentId: "nav-rail-zone",
                elementType: "nav-button",
                initialProperties: new Dictionary<string, object>
                {
                    { "Label", featureName },
                    { "TargetContainerId", contentContainerId },
                    { "Icon", icon }
                }
            ));
            _logger.LogInformation($"Created nav button for {featureName} with ID: {navButtonId}");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Host Feature UI Service is stopping.");
            return Task.CompletedTask;
        }
    }
}