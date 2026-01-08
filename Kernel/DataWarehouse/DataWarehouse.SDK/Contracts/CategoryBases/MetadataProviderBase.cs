namespace DataWarehouse.SDK.Contracts.CategoryBases
{
    /// <summary>
    /// Base class for Metadata/Indexing plugins.
    /// Handles indexing and searching metadata for stored data.
    /// Scale-agnostic: works from few files (SQLite) to trillions (Elasticsearch).
    ///
    /// Plugins only implement backend-specific index/search logic.
    /// Common operations (index/get/search/update/delete) handled by base.
    /// </summary>
    /// <remarks>Constructs metadata provider</remarks>
    public abstract class MetadataProviderBase(string id, string name, Version version) : PluginBase(id, name, version, PluginCategory.Metadata)
    {

        // =========================================================================
        // ABSTRACT MEMBERS
        // =========================================================================

        /// <summary>Index type (e.g., "sqlite", "postgres", "elasticsearch")</summary>
        protected abstract string IndexType { get; }

        /// <summary>Initialize index backend</summary>
        protected abstract Task InitializeIndexAsync(IKernelContext context);

        /// <summary>Insert/update index entry</summary>
        protected abstract Task UpsertIndexEntryAsync(string key, Dictionary<string, object> metadata);

        /// <summary>Read index entry</summary>
        protected abstract Task<Dictionary<string, object>?> GetIndexEntryAsync(string key);

        /// <summary>Delete index entry</summary>
        protected abstract Task DeleteIndexEntryAsync(string key);

        /// <summary>Execute search query</summary>
        protected abstract Task<List<Dictionary<string, object>>> ExecuteSearchAsync(string query);

        // =========================================================================
        // VIRTUAL MEMBERS
        // =========================================================================

        /// <summary>Whether this index supports SQL/advanced queries</summary>
        protected virtual bool SupportsAdvancedQueries => false;

        /// <summary>Max entries this index handles efficiently</summary>
        protected virtual long MaxEntries => long.MaxValue;

        /// <summary>Custom initialization</summary>
        protected virtual void InitializeMetadata(IKernelContext context) { }

        // =========================================================================
        // CAPABILITIES
        // =========================================================================

        /// <summary>Declares indexing capabilities</summary>
        protected override PluginCapabilityDescriptor[] Capabilities
        {
            get
            {
                var caps = new List<PluginCapabilityDescriptor>
                {
                    new()
                    {
                        CapabilityId = $"metadata.{IndexType}.index",
                        DisplayName = "Index Metadata",
                        Description = $"Index metadata in {IndexType}",
                        Category = CapabilityCategory.Metadata,
                        RequiredPermission = Security.Permission.Write,
                        Tags = ["metadata", "index", IndexType]
                    },
                    new()
                    {
                        CapabilityId = $"metadata.{IndexType}.get",
                        DisplayName = "Get Metadata",
                        Description = $"Retrieve metadata from {IndexType}",
                        Category = CapabilityCategory.Metadata,
                        RequiredPermission = Security.Permission.Read,
                        Tags = ["metadata", "get", IndexType]
                    },
                    new()
                    {
                        CapabilityId = $"metadata.{IndexType}.search",
                        DisplayName = "Search Metadata",
                        Description = $"Search metadata in {IndexType}",
                        Category = CapabilityCategory.Metadata,
                        RequiredPermission = Security.Permission.Read,
                        Tags = ["metadata", "search", IndexType]
                    }
                };

                if (SupportsAdvancedQueries)
                {
                    caps.Add(new()
                    {
                        CapabilityId = $"metadata.{IndexType}.query",
                        DisplayName = "Advanced Query",
                        Description = $"Execute SQL/advanced queries on {IndexType}",
                        Category = CapabilityCategory.Metadata,
                        RequiredPermission = Security.Permission.Read,
                        Tags = ["metadata", "query", "sql", IndexType]
                    });
                }

                return [.. caps];
            }
        }

        // =========================================================================
        // INITIALIZATION
        // =========================================================================

        /// <summary>Initializes and registers handlers</summary>
        protected override void InitializeInternal(IKernelContext context)
        {
            InitializeIndexAsync(context).GetAwaiter().GetResult();

            RegisterCapability($"metadata.{IndexType}.index", async (parameters) =>
            {
                var key = (string)parameters["key"];
                var metadata = (Dictionary<string, object>)parameters["metadata"];
                metadata["indexed_at"] = DateTime.UtcNow; // Add timestamp
                await UpsertIndexEntryAsync(key, metadata);
                return new { success = true, key };
            });

            RegisterCapability($"metadata.{IndexType}.get", async (parameters) =>
            {
                var key = (string)parameters["key"];
                return await GetIndexEntryAsync(key);
            });

            RegisterCapability($"metadata.{IndexType}.search", async (parameters) =>
            {
                var query = (string)parameters["query"];
                var results = await ExecuteSearchAsync(query);
                return new { results, count = results.Count };
            });

            if (SupportsAdvancedQueries)
            {
                RegisterCapability($"metadata.{IndexType}.query", async (parameters) =>
                {
                    var sql = (string)parameters["sql"];
                    var results = await ExecuteSearchAsync(sql);
                    return new { results, count = results.Count };
                });
            }

            InitializeMetadata(context);
        }
    }
}
