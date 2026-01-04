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