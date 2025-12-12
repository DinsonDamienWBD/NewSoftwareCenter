using SoftwareCenter.Core.Commands;

namespace SoftwareCenter.Module.AI.Agent.Commands
{
    public class NaturalLanguageCommand : ICommand<string>
    {
        public string Input { get; }

        public NaturalLanguageCommand(string input)
        {
            Input = input;
        }
    }
}
