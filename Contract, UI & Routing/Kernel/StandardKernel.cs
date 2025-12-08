using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Jobs;
using SoftwareCenter.Kernel.Contracts;
using SoftwareCenter.Kernel.Data;
using SoftwareCenter.Kernel.Services;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace SoftwareCenter.Kernel
{
    public class StandardKernel : IKernel, IDisposable
    {
        public IRouter Router { get; }
        public IGlobalDataStore DataStore { get; }
        public IEventBus EventBus { get; }
        public IJobScheduler JobScheduler { get; }

        private readonly ServiceRegistry _registry;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<StandardKernel> _logger;
        private readonly ModuleLoader _loader;
        private readonly IServiceCollection _services;

        public StandardKernel(ILoggerFactory loggerFactory, IServiceCollection services)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = _loggerFactory.CreateLogger<StandardKernel>();
            _services = services;

            _registry = new ServiceRegistry();
            DataStore = new GlobalDataStore();
            EventBus = new EventBus(_loggerFactory.CreateLogger<EventBus>());
            Router = new CommandBus(_registry, EventBus);
            JobScheduler = new JobScheduler(_loggerFactory.CreateLogger<JobScheduler>());
            _loader = new ModuleLoader(this, _registry, services);

            RegisterService<ILoggerFactory>(_loggerFactory);
            RegisterService<IGlobalDataStore>(DataStore);
            RegisterService<IEventBus>(EventBus);
            RegisterService<IJobScheduler>(JobScheduler);
            RegisterService<IRouter>(Router);
            RegisterService<IKernel>(this);

            RegisterSystemCommands();
        }

        private void RegisterSystemCommands()
        {
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

        public void Register(string commandName, Func<ICommand, Task<IResult>> handler, RouteMetadata metadata)
        {
            _registry.Register(commandName, handler, metadata, 0);
        }

        public Task<IResult> RouteAsync(ICommand command)
        {
            return Router.RouteAsync(command);
        }

        public T GetService<T>() where T : class
        {
            //This is a simple service locator. A more robust solution would use the IServiceProvider.
            var provider = _services.BuildServiceProvider();
            return provider.GetService<T>();
        }

        public void RegisterService<T>(T service) where T : class
        {
            _services.AddSingleton(service);
        }

        public async Task StartAsync()
        {
            var modulesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Modules");
            Directory.CreateDirectory(modulesPath);
            _logger.LogInformation("Kernel starting...");
            await _loader.LoadModulesAsync(modulesPath);
            _logger.LogInformation("Kernel ready. All modules loaded.");
        }

        public void Dispose()
        {
            (DataStore as IDisposable)?.Dispose();
            (JobScheduler as IDisposable)?.Dispose();
        }
    }
}