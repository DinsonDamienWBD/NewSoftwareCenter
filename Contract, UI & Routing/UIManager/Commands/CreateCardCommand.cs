using SoftwareCenter.Core.Commands;

namespace SoftwareCenter.UIManager.Commands
{
    public class CreateCardCommand : ICommand
    {
        public string Title { get; set; }
        public string ContainerId { get; set; }
    }
}