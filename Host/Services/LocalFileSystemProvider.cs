using Core.Services;

namespace Host.Services
{
    /// <summary>
    /// File system provider that interacts with the local file system.
    /// </summary>
    public class LocalFileSystemProvider : IFileSystemProvider
    {
        // For security, we might want to restrict this to AppContext.BaseDirectory in V2

        /// <summary>
        /// Checks if a file exists at the specified path.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task<bool> FileExistsAsync(string path, CancellationToken ct)
            => Task.FromResult(File.Exists(path));

        /// <summary>
        /// Reads all text from a file asynchronously.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<string> ReadTextAsync(string path, CancellationToken ct)
            => await File.ReadAllTextAsync(path, ct);

        /// <summary>
        /// Writes text to a file asynchronously.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="content"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task WriteTextAsync(string path, string content, CancellationToken ct)
            => await File.WriteAllTextAsync(path, content, ct);

        /// <summary>
        /// Deletes a file asynchronously.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task DeleteFileAsync(string path, CancellationToken ct)
        {
            if (File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets files in a directory matching a pattern asynchronously.
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="pattern"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task<IEnumerable<string>> GetFilesAsync(string directory, string pattern, CancellationToken ct)
        {
            if (!Directory.Exists(directory)) return Task.FromResult(Enumerable.Empty<string>());
            return Task.FromResult((IEnumerable<string>)Directory.GetFiles(directory, pattern));
        }

        /// <summary>
        /// Creates a directory asynchronously.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task CreateDirectoryAsync(string path, CancellationToken ct)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return Task.CompletedTask;
        }
    }
}