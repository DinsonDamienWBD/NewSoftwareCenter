namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// Serializer
    /// </summary>
    public interface ISerializer
    {
        /// <summary>
        /// Serialize
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        string Serialize<T>(T value);

        /// <summary>
        /// Deserialize
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        T? Deserialize<T>(string value);

        /// <summary>
        /// Deserialize
        /// </summary>
        /// <param name="value"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        object? Deserialize(string value, Type type);

        /// <summary>
        /// Serialize
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        Task SerializeAsync<T>(Stream stream, T value);

        /// <summary>
        /// Deserialize
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream"></param>
        /// <returns></returns>
        Task<T?> DeserializeAsync<T>(Stream stream);
    }
}