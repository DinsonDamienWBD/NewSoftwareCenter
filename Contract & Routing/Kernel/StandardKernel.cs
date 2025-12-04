using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Data;
using SoftwareCenter.Core.Events;
using SoftwareCenter.Core.Jobs;
using SoftwareCenter.Core.Kernel;
using SoftwareCenter.Core.Routing;
using SoftwareCenter.Kernel.Engine;
using SoftwareCenter.Kernel.Routing;
using SoftwareCenter.Kernel.Services;

namespace SoftwareCenter.Kernel
{
    /// <summary>
    /// Feature 7: The Concrete Kernel.
    /// The "Composition Root" for the Brain.
    /// Implements IKernel, DI, and Module Loading.
    /// </summary>
    public class StandardKernel : IKernel, IDisposable
    {
        // 1. Services
        public IRouter Router { get; }
        public IGlobalDataStore DataStore { get; }
        public IEventBus EventBus { get; }
        public IJobScheduler JobScheduler { get; }

        // 2. Internals
        private readonly HandlerRegistry _registry;
        private readonly KernelLogger _logger;
        private readonly ModuleLoader _loader;

        // 3. DI Container (Lightweight)
        private readonly Dictionary<Type, object> _services = new();

        public StandardKernel()
        {
            // A. Bootstrap State & Bus
            _registry = new HandlerRegistry();
            DataStore = new GlobalDataStore();
            EventBus = new DefaultEventBus();

            // B. Bootstrap Observability
            _logger = new KernelLogger(DataStore, EventBus);

            // C. Bootstrap Intelligence (Router & Scheduler)
            Router = new SmartRouter(_registry, EventBus);
            JobScheduler = new JobScheduler(_logger);

            // D. Bootstrap Engine
            _loader = new ModuleLoader(this, _registry);

            // E. Register Self for DI
            RegisterService<IGlobalDataStore>(DataStore);
            RegisterService<IEventBus>(EventBus);
            RegisterService<IJobScheduler>(JobScheduler);
            RegisterService<IRouter>(Router);
            RegisterService<IKernel>(this);

            // F. Register Internal System Commands (The Missing Link)
            RegisterSystemCommands();
        }

        private void RegisterSystemCommands()
        {
            // 1. System.Log.Config: Allows Runtime toggling of Verbose Logging
            _registry.Register(
                "System.Log.Config",
                async (cmd) =>
                {
                    if (cmd.Parameters.TryGetValue("Verbose", out var v) && v is bool verbose)
                    {
                        // Persist the setting
                        await DataStore.StoreAsync("Settings.VerboseLogging", verbose, DataPolicy.Persistent);
                        // Invalidate local cache
                        _logger.RefreshSettings();
                        return Result.FromSuccess(verbose, $"Verbose Logging set to {verbose}");
                    }
                    return Result.FromFailure("Invalid Parameters. Expected 'Verbose' (bool).");
                },
                new RouteMetadata
                {
                    CommandId = "System.Log.Config",
                    Description = "Toggles Verbose Logging. Params: { 'Verbose': bool }",
                    SourceModule = "Kernel",
                    Version = "1.0.0"
                },
                100 // High Priority
            );

            // 2. System.Help: The Discovery API
            _registry.Register(
                "System.Help",
                (cmd) =>
                {
                    var manifest = _registry.GetRegistryManifest();
                    return Task.FromResult<IResult>(Result.FromSuccess(manifest));
                },
                new RouteMetadata
                {
                    CommandId = "System.Help",
                    Description = "Returns a manifest of all registered commands.",
                    SourceModule = "Kernel",
                    Version = "1.0.0"
                },
                100
            );
        }

        // --- IKernel Implementation ---

        public void Register(string commandName, Func<ICommand, Task<IResult>> handler, RouteMetadata metadata)
        {
            // Default priority 0. Modules can cast to specific Registry types if they need advanced priority.
            _registry.Register(commandName, handler, metadata, 0);
        }

        public Task<IResult> RouteAsync(ICommand command)
        {
            return Router.RouteAsync(command);
        }

        public T GetService<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service))
            {
                return (T)service;
            }
            throw new InvalidOperationException($"Service {typeof(T).Name} is not registered in Kernel.");
        }

        // --- Lifecycle & Helpers ---

        private void RegisterService<T>(T implementation)
        {
            _services[typeof(T)] = implementation!;
        }

        public async Task StartAsync()
        {
            // Load standard modules from ./Modules
            var modulesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Modules");

            // Ensure directory exists to prevent crashes on fresh installs
            Directory.CreateDirectory(modulesPath);

            // Log start
            await _logger.LogExecutionAsync(new JobCommandStub("Kernel.Boot"), true, 0);

            // Load
            await _loader.LoadModulesAsync(modulesPath);

            await _logger.LogExecutionAsync(new JobCommandStub("Kernel.Ready"), true, 0);
        }

        public void Dispose()
        {
            (DataStore as IDisposable)?.Dispose();
            (JobScheduler as IDisposable)?.Dispose();
        }

        // Helper stub for internal Kernel logs
        private class JobCommandStub : ICommand
        {
            public string Name { get; }
            public Dictionary<string, object> Parameters { get; } = new();
            public Guid TraceId { get; } = Guid.NewGuid();
            public List<Core.Diagnostics.TraceHop> History { get; } = new();
            public JobCommandStub(string name) { Name = name; }
        }
    }
}