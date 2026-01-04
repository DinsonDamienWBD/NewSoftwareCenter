using DataWarehouse.SDK.Contracts;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataWarehouse.Kernel.Serialization
{
    /// <summary>
    /// Standard implementation of ISerializer using System.Text.Json.
    /// Configured for maximum compatibility and strictness.
    /// </summary>
    public class JsonSerializerAdapter : ISerializer
    {
        private readonly JsonSerializerOptions _options;

        /// <summary>
        /// Constructor
        /// </summary>
        public JsonSerializerAdapter()
        {
            _options = new JsonSerializerOptions
            {
                WriteIndented = true, // Readable JSON on disk
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() } // Store enums as Strings, not Integers
            };
        }

        /// <summary>
        /// Serialize
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public string Serialize<T>(T value)
            => JsonSerializer.Serialize(value, _options);

        /// <summary>
        /// Deserialize
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public T? Deserialize<T>(string value)
            => JsonSerializer.Deserialize<T>(value, _options);

        /// <summary>
        /// Deserialize with type
        /// </summary>
        /// <param name="value"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public object? Deserialize(string value, Type type)
            => JsonSerializer.Deserialize(value, type, _options);

        /// <summary>
        /// Serialize async
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task SerializeAsync<T>(Stream stream, T value)
            => await JsonSerializer.SerializeAsync(stream, value, _options);

        /// <summary>
        /// Deserialize async
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream"></param>
        /// <returns></returns>
        public async Task<T?> DeserializeAsync<T>(Stream stream)
            => await JsonSerializer.DeserializeAsync<T>(stream, _options);
    }
}