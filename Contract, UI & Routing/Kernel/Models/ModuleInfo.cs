using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;
using SoftwareCenter.Core.Modules;
using SoftwareCenter.Kernel.Services;

namespace SoftwareCenter.Kernel.Models
{
    public class ModuleInfo
    {
        public string ModuleId { get; }
        public Assembly Assembly { get; }
        public ModuleLoadContext LoadContext { get; }
        public IModule Instance { get; set; }
        public ModuleState State { get; set; }
        public List<ModuleLoader.DiscoveredHandler> Handlers { get; } = new List<ModuleLoader.DiscoveredHandler>();
        public List<Type> ApiEndpoints { get; } = new List<Type>();
        public List<Type> Services { get; } = new List<Type>();

        public ModuleInfo(string moduleId, Assembly assembly, ModuleLoadContext loadContext)
        {
            ModuleId = moduleId;
            Assembly = assembly;
            LoadContext = loadContext;
            State = ModuleState.Loaded;
        }
    }

    public enum ModuleState
    {
        Loaded,
        Initializing,
        Initialized,
        Unloading,
        Unloaded,
        Error
    }
}
