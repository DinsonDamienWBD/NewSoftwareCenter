using SoftwareCenter.Kernel.Contracts;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SoftwareCenter.Kernel.Services
{
    public class GlobalDataStore : IGlobalDataStore
    {
        private readonly Dictionary<string, object> _transientStore = new Dictionary<string, object>();

        public Task<bool> ExistsAsync(string key)
        {
            return Task.FromResult(_transientStore.ContainsKey(key));
        }

        public Task<DataEntry<object>?> GetMetadataAsync(string key)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RemoveAsync(string key)
        {
            return Task.FromResult(_transientStore.Remove(key));
        }

        public Task<DataEntry<T>?> RetrieveAsync<T>(string key)
        {
            if (_transientStore.TryGetValue(key, out var value))
            {
                return Task.FromResult(new DataEntry<T>
                {
                    Value = (T)value,
                    DataType = typeof(T).Name,
                    SourceId = "Kernel",
                    LastUpdated = DateTime.UtcNow
                });
            }
            return Task.FromResult<DataEntry<T>?>(null);
        }

        public Task<bool> StoreAsync<T>(string key, T data, DataPolicy policy = DataPolicy.Transient)
        {
            _transientStore[key] = data;
            return Task.FromResult(true);
        }

        public Task<bool> StoreBulkAsync<T>(IDictionary<string, T> items, DataPolicy policy = DataPolicy.Transient)
        {
            foreach (var item in items)
            {
                _transientStore[item.Key] = item.Value;
            }
            return Task.FromResult(true);
        }
    }
}