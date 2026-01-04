namespace Host.Adapter
{
    /// <summary>
    /// Adapter for DataWarehouse
    /// </summary>
    public class DataWarehouseToHostAdapter
    {
        private readonly DataWarehouseWarehouse _warehouse;

        public DataWarehouseToHostAdapter(string rootPath)
        {
            _warehouse = new DataWarehouseWarehouse(rootPath);
        }

        // Map Host calls to DataWarehouse calls
        public async Task SaveDataAsync(string userId, string key, Stream data)
        {
            // Create a security context wrapper on the fly
            var context = new SimpleSecurityContext(userId);

            // Map 'key' to container/blob logic
            await _warehouse.StoreBlobAsync(context, "default", key, data);
        }

        // ... Implement other Host methods ...

        private record SimpleSecurityContext(string UserId) : ISecurityContext
        {
            public string TenantId => "default";
            public IEnumerable<string> Roles => Array.Empty<string>();
            public bool IsSystemAdmin => false;
        }
    }
}
