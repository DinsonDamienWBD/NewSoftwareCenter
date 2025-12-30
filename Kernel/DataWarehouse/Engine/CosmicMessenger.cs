using DataWarehouse.Contracts;
using DataWarehouse.Contracts.Messages;
using DataWarehouse.Engine;
using Microsoft.Win32;
using System;
using System.Threading.Tasks;

namespace DataWarehouse.Messaging
{
    /// <summary>
    /// The Universal Port.
    /// You can throw ANY supported message at this class, and it routes it to the Engine.
    /// This allows usage via HTTP Controllers, gRPC, MassTransit, or MediatR.
    /// The universal entry point. It translates Messages into Engine calls.
    /// </summary>
    public class CosmicMessenger(CosmicWarehouse engine)
    {
        private readonly CosmicWarehouse _engine = engine;

        // --- The "Magic" Switch ---
        // In a real CQRS framework (MediatR), this dispatch happens automatically via Reflection.
        // Here is the explicit logic for transparency.

        /// <summary>
        /// Handle
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public async Task<object> HandleAsync(object message)
        {
            return message switch
            {
                // Commands
                StoreBlobCommand cmd => await HandleStore(cmd),
                DeleteBlobCommand cmd => await HandleDelete(cmd),
                MemorizeFactCommand cmd => await HandleMemorize(cmd),
                LinkRemoteStorageCommand cmd => await HandleLink(cmd),
                UnlinkRemoteStorageCommand cmd => await HandleUnlink(cmd),
                GetLinkedResourcesQuery q => await HandleGetLinks(q),
                ToggleFeatureCommand cmd => HandleToggle(cmd),

                // Queries
                GetBlobQuery q => await HandleGet(q),
                SearchMemoriesQuery q => await HandleSearch(q),
                GetFeatureStatusQuery q => HandleGetFeatures(q),

                _ => throw new NotSupportedException($"Unknown message type: {message.GetType().Name}")
            };
        }

        // --- Internal Handlers ---

        private async Task<BlobStoredEvent> HandleStore(StoreBlobCommand cmd)
        {
            var intent = cmd.Intent ?? new StorageIntent();

            // FIX: Pass Manifest (Metadata) correctly.
            // If cmd.Metadata is null, we pass null.
            await _engine.StoreObjectAsync(cmd.Bucket, cmd.Key, cmd.Data, intent, cmd.Metadata, cmd.ExpectedETag);

            return new BlobStoredEvent($"file:///{cmd.Bucket}/{cmd.Key}", "new-etag", cmd.Data.Length);
        }

        private async Task<object> HandleGet(GetBlobQuery q)
        {
            return await _engine.RetrieveObjectAsync(q.Bucket, q.Key);
        }

        private Task<object> HandleDelete(DeleteBlobCommand _) => throw new NotImplementedException();

        private async Task<string> HandleMemorize(MemorizeFactCommand cmd)
        {
            return await _engine.MemorizeAsync(cmd.Content, cmd.Tags);
        }

        private async Task<string[]> HandleSearch(SearchMemoriesQuery q)
        {
            return await _engine.SearchMemoriesAsync(q.Text, null, q.Limit);
        }

        // Handlers:
        private async Task<object> HandleLink(LinkRemoteStorageCommand cmd)
        {
            _engine.LinkResource(cmd.Alias, cmd.Address);
            return Task.CompletedTask;
        }

        private async Task<object> HandleUnlink(UnlinkRemoteStorageCommand cmd)
        {
            _engine.UnlinkResource(cmd.Alias);
            return Task.CompletedTask;
        }

        private Task<object> HandleGetLinks(GetLinkedResourcesQuery _)
            => Task.FromResult<object>(_engine.GetLinkedResources());

        private Task<object> HandleToggle(ToggleFeatureCommand cmd)
        {
            _engine._features.SetFeatureState(cmd.PluginId, cmd.IsEnabled);
            return Task.FromResult<object>(true);
        }

        private Task<Dictionary<string, bool>> HandleGetFeatures(GetFeatureStatusQuery _)
        {
            var installed = _engine._registry.GetAllPluginIds();
            return Task.FromResult(_engine._features.GetAllStates(installed));
        }
    }
}