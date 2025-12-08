using System;
using System.Collections.Generic;
using System.Text.Json;
using LiteDB;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.Kernel.Data; // Added for AccessPermissions

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// Provides a shared, persistent data store for modules with ownership and audit tracing.
    /// </summary>
    public class GlobalDataStore : IDisposable
    {
        private readonly LiteDatabase _db;
        private readonly ILiteCollection<BsonDocument> _dataCollection;
        private readonly ILiteCollection<AuditRecord> _auditCollection;

        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalDataStore"/> class.
        /// </summary>
        /// <param name="databasePath">The path to the LiteDB database file.</param>
        public GlobalDataStore(string databasePath = "global_store.db")
        {
            _db = new LiteDatabase(databasePath);
            _dataCollection = _db.GetCollection("datastore");
            _dataCollection.EnsureIndex(x => x["Key"]);

            _auditCollection = _db.GetCollection<AuditRecord>("auditlog");
            _auditCollection.EnsureIndex(x => x.DataKey);
            _auditCollection.EnsureIndex(x => x.TraceId);
            _auditCollection.EnsureIndex(x => x.InitiatingModuleId);
        }

        public T Get<T>(string key, ITraceContext traceContext)
        {
            var doc = _dataCollection.FindOne(Query.EQ("Key", key));
            
            // Log the read operation
            LogAudit(key, "Get", traceContext);

            if (doc == null)
            {
                return default;
            }

            var storeItem = BsonMapper.Global.ToObject<StoreItem<T>>(doc);
            
            var requestingModuleId = GetInitiatingModuleId(traceContext);
            if (storeItem.OwnerModuleId != requestingModuleId && !HasPermission(storeItem, requestingModuleId, AccessPermissions.Read))
            {
                throw new UnauthorizedAccessException($"Module '{requestingModuleId}' does not have read access to key '{key}'.");
            }

            return storeItem.Value;
        }

        public void Set<T>(string key, T value, string ownerModuleId, ITraceContext traceContext)
        {
            var existingDoc = _dataCollection.FindOne(Query.EQ("Key", key));
            
            LogAudit(key, "Set", traceContext, new { OldValueExists = existingDoc != null, NewValue = value });

            if (existingDoc != null)
            {
                var existingItem = BsonMapper.Global.ToObject<StoreItem<T>>(existingDoc);
                
                var requestingModuleId = GetInitiatingModuleId(traceContext);
                if (existingItem.OwnerModuleId != requestingModuleId && !HasPermission(existingItem, requestingModuleId, AccessPermissions.Write))
                {
                    throw new UnauthorizedAccessException($"Module '{requestingModuleId}' does not have write access to key '{key}'.");
                }

                existingItem.Value = value;
                existingItem.LastUpdatedAt = DateTimeOffset.UtcNow;
                existingItem.LastUpdaterTraceId = traceContext.TraceId;
                _dataCollection.Update(BsonMapper.Global.ToBson(existingItem));
            }
            else
            {
                var newItem = new StoreItem<T>
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    Value = value,
                    OwnerModuleId = ownerModuleId,
                    CreatorTraceId = traceContext.TraceId,
                    CreatedAt = DateTimeOffset.UtcNow,
                    LastUpdaterTraceId = traceContext.TraceId,
                    LastUpdatedAt = DateTimeOffset.UtcNow
                };
                _dataCollection.Insert(BsonMapper.Global.ToBson(newItem));
            }
        }

        public void Delete(string key, ITraceContext traceContext)
        {
            var doc = _dataCollection.FindOne(Query.EQ("Key", key));
            if (doc == null) return; // Nothing to delete

            LogAudit(key, "Delete", traceContext);

            var storeItem = BsonMapper.Global.ToObject<StoreItem<object>>(doc); // Use object as T for generic operations

            var requestingModuleId = GetInitiatingModuleId(traceContext);
            if (storeItem.OwnerModuleId != requestingModuleId && !HasPermission(storeItem, requestingModuleId, AccessPermissions.Delete))
            {
                throw new UnauthorizedAccessException($"Module '{requestingModuleId}' does not have delete access to key '{key}'.");
            }

            _dataCollection.Delete(doc["_id"].AsGuid);
        }

        public void ShareData(string key, string targetModuleId, AccessPermissions permissions, ITraceContext traceContext)
        {
            var doc = _dataCollection.FindOne(Query.EQ("Key", key));
            if (doc == null)
            {
                throw new KeyNotFoundException($"Key '{key}' not found in Global Data Store.");
            }

            LogAudit(key, "Share", traceContext, new { TargetModule = targetModuleId, Permissions = permissions });

            var storeItem = BsonMapper.Global.ToObject<StoreItem<object>>(doc);

            var requestingModuleId = GetInitiatingModuleId(traceContext);
            if (storeItem.OwnerModuleId != requestingModuleId && !HasPermission(storeItem, requestingModuleId, AccessPermissions.Share))
            {
                throw new UnauthorizedAccessException($"Module '{requestingModuleId}' does not have permission to share key '{key}'.");
            }

            storeItem.SharedPermissions[targetModuleId] = permissions;
            _dataCollection.Update(BsonMapper.Global.ToBson(storeItem));
        }

        public void TransferOwnership(string key, string newOwnerModuleId, ITraceContext traceContext)
        {
            var doc = _dataCollection.FindOne(Query.EQ("Key", key));
            if (doc == null)
            {
                throw new KeyNotFoundException($"Key '{key}' not found in Global Data Store.");
            }

            LogAudit(key, "TransferOwnership", traceContext, new { OldOwner = BsonMapper.Global.ToObject<StoreItem<object>>(doc).OwnerModuleId, NewOwner = newOwnerModuleId });

            var storeItem = BsonMapper.Global.ToObject<StoreItem<object>>(doc);

            var requestingModuleId = GetInitiatingModuleId(traceContext);
            if (storeItem.OwnerModuleId != requestingModuleId && !HasPermission(storeItem, requestingModuleId, AccessPermissions.TransferOwnership))
            {
                throw new UnauthorizedAccessException($"Module '{requestingModuleId}' does not have permission to transfer ownership of key '{key}'.");
            }

            storeItem.OwnerModuleId = newOwnerModuleId;
            storeItem.SharedPermissions.Clear(); // Ownership transfer typically clears previous shares
            _dataCollection.Update(BsonMapper.Global.ToBson(storeItem));
        }

        private bool HasPermission(StoreItem<object> item, string moduleId, AccessPermissions requiredPermission)
        {
            if (item.SharedPermissions.TryGetValue(moduleId, out var grantedPermissions))
            {
                return grantedPermissions.HasFlag(requiredPermission);
            }
            return false;
        }

        private string GetInitiatingModuleId(ITraceContext traceContext)
        {
            if (traceContext.Items.TryGetValue("ModuleId", out var moduleIdObj) && moduleIdObj is string moduleId)
            {
                return moduleId;
            }
            return "Unknown";
        }

        private void LogAudit(string dataKey, string operationType, ITraceContext traceContext, object context = null)
        {
            var initiatingModuleId = GetInitiatingModuleId(traceContext);

            var auditRecord = new AuditRecord
            {
                Id = Guid.NewGuid(),
                DataKey = dataKey,
                OperationType = operationType,
                Timestamp = DateTimeOffset.UtcNow,
                TraceId = traceContext.TraceId,
                InitiatingModuleId = initiatingModuleId,
                Context = context != null ? JsonSerializer.Serialize(context) : string.Empty
            };
            _auditCollection.Insert(auditRecord);
        }

        public void Dispose()
        {
            _db?.Dispose();
        }
    }
}
