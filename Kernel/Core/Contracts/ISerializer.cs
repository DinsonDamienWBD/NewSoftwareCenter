namespace Core.Contracts
{
    /// <summary>
    /// Abstracts the serialization logic (JSON, MessagePack, etc).
    /// </summary>
    public interface ISerializer
    {
        /// <summary>
        /// Serialize a payload
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        string Serialize<T>(T value);

        /// <summary>
        /// Deserialize to a string
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        T? Deserialize<T>(string value);

        /// <summary>
        /// Serialize a stream
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        Task SerializeAsync<T>(Stream stream, T value);

        /// <summary>
        /// Deserialize to a stream
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream"></param>
        /// <returns></returns>
        Task<T?> DeserializeAsync<T>(Stream stream);

        /// <summary>
        /// Deserialize to an object
        /// </summary>
        /// <param name="value"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        object? Deserialize(string value, Type type);
    }
}