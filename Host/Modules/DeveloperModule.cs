using Core;
using Core.Backend.Contracts;
using Core.Backend.Messages;
using Core.Frontend.Contracts;
using Core.Modules.Contracts;
using Core.ServiceRegistry;

namespace Host.Modules
{
    /// <summary>
    /// Test module providing developer tools such as registry inspection.
    /// </summary>
    public class DeveloperModule : IModule
    {
        /// <summary>
        /// Module Identifier.
        /// </summary>
        public string ModuleId => "Developer";

        /// <summary>
        /// Module Name.
        /// </summary>
        public string ModuleName => "Developer Tools";

        /// <summary>
        /// Module Initialization Logic.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public Task InitializeAsync(ModuleContext context) => Task.CompletedTask;

        /// <summary>
        /// Register Backend and Frontend Handlers.
        /// </summary>
        /// <param name="backend"></param>
        /// <param name="frontend"></param>
        public void Register(IBackendRegistry backend, IFrontendRegistry frontend)
        {
            // Register the Help Query
            backend.RegisterHandler<GetRegistryHelpQuery, Result<List<RegistryItemMetadata>>, DeveloperHandler>("system/dev/help");
        }

        /// <summary>
        /// Startup Logic.
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        /// <summary>
        /// Shutdown Logic.
        /// </summary>
        /// <returns></returns>
        public Task ShutdownAsync() => Task.CompletedTask;

        /// <summary>
        /// Verification Logic.
        /// </summary>
        /// <returns></returns>
        public Task<bool> VerifyAsync() => Task.FromResult(true);
    }

    // --- MESSAGES ---
    /// <summary>
    /// Query to get help information about all registered backend and frontend items.
    /// </summary>
    public class GetRegistryHelpQuery : MessageBase, IQuery<Result<List<RegistryItemMetadata>>> { }

    /// <summary>
    /// Get Registry Help Handler
    /// </summary>
    /// <param name="backend"></param>
    /// <param name="frontend"></param>
    public class DeveloperHandler(IBackendRegistry backend, IFrontendRegistry frontend)
        : IHandler<GetRegistryHelpQuery, Result<List<RegistryItemMetadata>>>
    {
        private readonly IBackendRegistry _backend = backend;
        private readonly IFrontendRegistry _frontend = frontend;

        /// <summary>
        /// Handle GetRegistryHelpQuery
        /// </summary>
        /// <param name="query"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task<Result<List<RegistryItemMetadata>>> HandleAsync(GetRegistryHelpQuery query, CancellationToken ct)
        {
            var allData = new List<RegistryItemMetadata>();

            // 1. Harvest Backend
            allData.AddRange(_backend.GetRegistryDump());

            // 2. Harvest Frontend
            allData.AddRange(_frontend.GetRegistryDump());

            // 3. Sort for UI Clarity
            var sorted = allData
                .OrderBy(x => x.OwnerId)
                .ThenBy(x => x.Type)
                .ThenBy(x => x.Route)
                .ToList();

            return Task.FromResult(Result<List<RegistryItemMetadata>>.Success(sorted));
        }
    }
}