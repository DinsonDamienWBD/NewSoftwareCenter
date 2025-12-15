using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Commands.UI;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.Core.UI;
using SoftwareCenter.UIManager.Services;
using System.Threading.Tasks;

namespace SoftwareCenter.UIManager.Handlers
{
    public class ProcessUIManifestCommandHandler : ICommandHandler<ProcessUIManifestCommand>
    {
        private readonly global::SoftwareCenter.Core.UI.IUiService _uiService;
        private readonly IUIHubNotifier _hubNotifier;

        public ProcessUIManifestCommandHandler(global::SoftwareCenter.Core.UI.IUiService uiService, IUIHubNotifier hubNotifier)
        {
            _uiService = uiService;
            _hubNotifier = hubNotifier;
        }

        public async Task Handle(ProcessUIManifestCommand command, ITraceContext traceContext)
        {
            var manifest = command.Manifest;

            // 1. Build the HTML on the server
            var htmlOutput = await _uiService.RenderManifestAsync(manifest);

            // 2. Send to Frontend via the Notifier
            if (manifest.Operation == "inject")
            {
                await _hubNotifier.InjectFragment(manifest.TargetGuid, manifest.MountPoint, htmlOutput);
            }
            else if (manifest.Operation == "update")
            {
                await _hubNotifier.UpdateFragment(manifest.TargetGuid, manifest.MountPoint, htmlOutput);
            }
            else if (manifest.Operation == "remove")
            {
                await _hubNotifier.RemoveFragment(manifest.TargetGuid);
            }
        }
    }
}
