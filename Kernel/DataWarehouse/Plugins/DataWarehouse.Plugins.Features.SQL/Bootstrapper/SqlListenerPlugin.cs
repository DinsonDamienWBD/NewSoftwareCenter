using DataWarehouse.Plugins.Features.SQL.Engine;
using DataWarehouse.Plugins.Features.SQL.Services;
using DataWarehouse.SDK.Contracts;

namespace DataWarehouse.Plugins.Features.SQL.Bootstrapper
{
    /// <summary>
    /// Bootstrapper for Postgres SQL Listener
    /// </summary>
    public class SqlListenerPlugin : IFeaturePlugin
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "Cosmic.Features.SQL";

        /// <summary>
        /// Version
        /// </summary>
        public string Version => "1.2.0";

        /// <summary>
        /// Name
        /// </summary>
        public string Name => "Postgres SQL Listener";

        private PostgresInterface? _interface;
        private PostgresWireProtocol? _protocol;
        private IKernelContext? _context;

        /// <summary>
        /// Handshake protocol handler
        /// </summary>
        public Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
        {
            _context = request as IKernelContext;
            var index = _context?.GetPlugin<IMetadataIndex>();
            if (index == null)
            {
                return Task.FromResult(HandshakeResponse.Failure(
                    Id,
                    Name,
                    "IMetadataIndex not found. SQL Listener cannot function."));
            }

            _interface = new PostgresInterface(index, new PostgresInterface.ContextLoggerAdapter<PostgresInterface>(_context!));
            _protocol = new PostgresWireProtocol(_interface, _context!);

            return Task.FromResult(HandshakeResponse.Success(
                pluginId: Id,
                name: Name,
                version: new Version(Version),
                category: PluginCategory.Feature
            ));
        }

        /// <summary>
        /// Message handler (optional).
        /// </summary>
        public Task OnMessageAsync(PluginMessage message)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(IKernelContext context)
        {
            var index = context.GetPlugin<IMetadataIndex>();
            if (index == null) return;

            // 1. Create Service
            _interface = new PostgresInterface(index, new PostgresInterface.ContextLoggerAdapter<PostgresInterface>(context));

            // 2. Create Wire Listener
            _protocol = new PostgresWireProtocol(_interface, context);
        }

        /// <summary>
        /// Start
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task StartAsync(CancellationToken ct)
        {
            _protocol?.Start(5432);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop
        /// </summary>
        /// <returns></returns>
        public Task StopAsync()
        {
            _protocol?.Dispose();
            return Task.CompletedTask;
        }
    }
}