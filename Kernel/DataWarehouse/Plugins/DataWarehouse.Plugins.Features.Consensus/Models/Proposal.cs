namespace DataWarehouse.Plugins.Features.Consensus.Models
{
    /// <summary>
    /// Proposal
    /// </summary>
    public class Proposal
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Command
        /// </summary>
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// Payload
        /// </summary>
        public byte[] Payload { get; set; } = [];
    }
}
