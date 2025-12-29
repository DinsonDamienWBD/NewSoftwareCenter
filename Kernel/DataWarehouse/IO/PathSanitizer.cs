namespace DataWarehouse.IO
{
    /// <summary>
    /// Security Helper to prevent Path Traversal attacks.
    /// Ensures all IO operations stay within the defined Root.
    /// </summary>
    internal static class PathSanitizer
    {
        /// <summary>
        /// Resolve a erlative path
        /// </summary>
        /// <param name="root"></param>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        /// <exception cref="UnauthorizedAccessException"></exception>
        public static string Resolve(string root, string relativePath)
        {
            var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));

            if (!fullPath.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"Access Denied: Path '{relativePath}' resolves outside the warehouse root.");
            }

            return fullPath;
        }
    }
}