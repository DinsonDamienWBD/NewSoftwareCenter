using Core.Messages;
using System.Text;

namespace Core.Extensions
{
    /// <summary>
    /// Helper for Stream
    /// </summary>
    public static class MessageExtensions
    {
        /// <summary>
        /// Read stream as bytes
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public static async Task<byte[]> ReadBytesAsync(this StreamMessage message)
        {
            if (message.DataStream == null) return [];
            using var ms = new MemoryStream();
            await message.DataStream.CopyToAsync(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Read stream as string
        /// </summary>
        /// <param name="message"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static async Task<string> ReadStringAsync(this StreamMessage message, Encoding? encoding = null)
        {
            if (message.DataStream == null) return string.Empty;
            using var reader = new StreamReader(message.DataStream, encoding ?? Encoding.UTF8, leaveOpen: true);
            // Reset position if possible, though streams are often forward-only in pipeline
            return await reader.ReadToEndAsync();
        }
    }
}