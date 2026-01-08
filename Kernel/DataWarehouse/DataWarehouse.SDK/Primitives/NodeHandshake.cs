namespace DataWarehouse.SDK.Primitives
{
    /// <summary>
    /// Node handshake rich object
    /// </summary>
    [Serializable]
    public class NodeHandshake
    {
        /// <summary>
        /// Success
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Node ID
        /// </summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>
        /// Cluster ID
        /// </summary>
        public string ClusterId { get; set; } = string.Empty;

        /// <summary>
        /// Version
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Capabilities
        /// </summary>
        public string[] Capabilities { get; set; } = [];

        /// <summary>
        /// Error message
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Handshake Failure
        /// </summary>
        /// <param name="error"></param>
        /// <returns></returns>
        public static NodeHandshake Failure(string error) => new() { Success = false, ErrorMessage = error };

        /// <summary>
        /// Handshake Success
        /// </summary>
        /// <param name="nodeId"></param>
        /// <returns></returns>
        public static NodeHandshake SuccessResult(string nodeId) => new() { Success = true, NodeId = nodeId };
    }
}