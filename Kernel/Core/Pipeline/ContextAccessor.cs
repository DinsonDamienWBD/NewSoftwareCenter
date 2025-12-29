namespace Core.Pipeline
{
    /// <summary>
    /// Helper interface to support cleaner passing of pipeline contexts
    /// </summary>
    public interface IPipelineContextAccessor
    {
        /// <summary>
        /// The context of the pipeline
        /// </summary>
        PipelineContext? Context { get; set; }
    }

    /// <summary>
    /// Implement pipeline context accessor
    /// </summary>
    public class PipelineContextAccessor : IPipelineContextAccessor
    {
        private static readonly AsyncLocal<PipelineContext?> _currentContext = new();

        /// <summary>
        /// Constructor
        /// </summary>
        public PipelineContext? Context
        {
            get => _currentContext.Value;
            set => _currentContext.Value = value;
        }
    }
}