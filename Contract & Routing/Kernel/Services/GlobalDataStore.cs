using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using LiteDB;
using SoftwareCenter.Core.Data;
using SoftwareCenter.Core.Diagnostics;

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// Implements Feature 5: Global Data Store.
    /// A Hybrid Store: Uses RAM for Transient data and LiteDB for Persistent data.
    /// Thread-safe and Async.
    /// </summary>
    public class GlobalDataStore : IGlobalDataStore, IDisposable
    {
        // 1. Persistent Layer (LiteDB)
        private readonly LiteDatabase _db;
        private readonly object _dbLock = new(); // LiteDB single-writer lock

        // 2. Transient Layer (RAM)
        // Key -> DataEntry<object>
        private readonly ConcurrentDictionary<string, object> _memoryStore = new();

        public GlobalDataStore()
        {
            // Path: %AppData%/SoftwareCenter/GlobalDataStore.db
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "SoftwareCenter");
            Directory.CreateDirectory(folder);

            var dbPath = Path.Combine(folder, "GlobalDataStore.db");

            // Open Database (Shared connection)
            _db = new LiteDatabase($"Filename={dbPath};Connection=Shared");

            // Configure Mapper
            BsonMapper.Global.IncludeFields = true;
        }

        public Task<bool> StoreAsync<T>(string key, T data, DataPolicy policy = DataPolicy.Transient)
        {
            return Task.Run(() =>
            {
                // 1. Create the Metadata Wrapper
                // Use TraceContext.Current to link this data to the active operation
                var entry = new DataEntry<T>
                {
                    Value = data,
                    LastUpdated = DateTime.UtcNow,
                    DataType = typeof(T).FullName ?? "Unknown",
                    TraceId = TraceContext.CurrentTraceId ?? Guid.Empty,
                    SourceId = "System" // Ideally passed via context
                };

                // 2. Store based on Policy
                if (policy == DataPolicy.Transient)
                {
                    _memoryStore[key] = entry;
                    return true;
                }
                else
                {
                    lock (_dbLock)
                    {
                        // FIX for CS1503: 
                        // Use non-generic GetCollection to work with BsonDocument directly
                        var col = _db.GetCollection("GlobalData");

                        var doc = BsonMapper.Global.ToDocument(entry);
                        doc["_id"] = key; // Manually force the Key as the Database ID

                        return col.Upsert(doc);
                    }
                }
            });
        }

        public Task<DataEntry<T>?> RetrieveAsync<T>(string key)
        {
            return Task.Run(() =>
            {
                // 1. Check RAM first (Fastest)
                if (_memoryStore.TryGetValue(key, out var memObj))
                {
                    if (memObj is DataEntry<T> typedEntry) return typedEntry;
                }

                // 2. Check Disk
                lock (_dbLock)
                {
                    var col = _db.GetCollection("GlobalData");
                    var doc = col.FindById(key);

                    if (doc != null)
                    {
                        // Deserialize back to DataEntry<T>
                        return BsonMapper.Global.ToObject<DataEntry<T>>(doc);
                    }
                }

                return null;
            });
        }

        public Task<bool> ExistsAsync(string key)
        {
            return Task.Run(() =>
            {
                if (_memoryStore.ContainsKey(key)) return true;

                lock (_dbLock)
                {
                    return _db.GetCollection("GlobalData").Exists(Query.EQ("_id", key));
                }
            });
        }

        public Task<bool> RemoveAsync(string key)
        {
            return Task.Run(() =>
            {
                bool removedFromMem = _memoryStore.TryRemove(key, out _);

                lock (_dbLock)
                {
                    // Use non-generic collection for deletion by ID
                    bool removedFromDb = _db.GetCollection("GlobalData").Delete(key);
                    return removedFromMem || removedFromDb;
                }
            });
        }

        public Task<DataEntry<object>?> GetMetadataAsync(string key)
        {
            return Task.Run(async () =>
            {
                // For simplicity, we reuse Retrieve. 
                // Optimization: In the future, fetch BsonDocument and map only metadata fields.
                var result = await RetrieveAsync<object>(key);
                return result;
            });
        }

        public void Dispose()
        {
            _db?.Dispose();
        }
    }
}