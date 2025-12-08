using SoftwareCenter.Core.Commands;

namespace SoftwareCenter.UIManager.Commands
{
    public class AddControlToContainerCommand : ICommand
    {
        public string ContainerId { get; set; }
        public string ControlType { get; set; }
        public string ControlId { get; set; }
    }
}