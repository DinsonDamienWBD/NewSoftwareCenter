using System.IO.MemoryMappedFiles;
using System.Text;

namespace DataWarehouse.Diagnostics
{
    /// <summary>
    /// Feature: High-speed ring buffer that survives crashes. Writes the last 1MB of activity to a Memory-Mapped File (MMF).
    /// </summary>
    public class FlightRecorder : IDisposable
    {
        private readonly MemoryMappedFile _mmf;
        private readonly MemoryMappedViewAccessor _accessor;
        private const int Size = 1024 * 1024; // 1MB Ring Buffer
        private long _position = 0;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="rootPath"></param>
        public FlightRecorder(string rootPath)
        {
            var path = Path.Combine(rootPath, "crash.dump");
            // Ensure file exists
            if (!File.Exists(path))
            {
                using var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                fs.SetLength(Size);
            }

            _mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Open, "FlightRecorder", Size, MemoryMappedFileAccess.ReadWrite);
            _accessor = _mmf.CreateViewAccessor();
        }

        /// <summary>
        /// Create a record
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="details"></param>
        public void Record(string operation, string details)
        {
            // Format: [Timestamp] [Op] [Details]\n
            var msg = $"{DateTime.UtcNow:O}|{operation}|{details}\n";
            var bytes = Encoding.UTF8.GetBytes(msg);

            // Ring Buffer Logic (Atomic increment)
            long startPos = Interlocked.Add(ref _position, bytes.Length) - bytes.Length;
            long offset = startPos % Size;

            if (offset + bytes.Length <= Size)
            {
                _accessor.WriteArray(offset, bytes, 0, bytes.Length);
            }
            else
            {
                // Wrap around
                long firstPart = Size - offset;
                _accessor.WriteArray(offset, bytes, 0, (int)firstPart);
                _accessor.WriteArray(0, bytes, (int)firstPart, (int)(bytes.Length - firstPart));
            }
        }

        /// <summary>
        /// Safely dispose
        /// </summary>
        public void Dispose()
        {
            _accessor.Dispose();
            _mmf.Dispose();
        }
    }
}