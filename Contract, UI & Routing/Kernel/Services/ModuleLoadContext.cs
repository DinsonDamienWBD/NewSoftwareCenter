using System;
using System.Reflection;
using System.Runtime.Loader;
using System.IO;

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// A custom AssemblyLoadContext for loading individual modules.
    /// This provides isolation for module dependencies and enables eventual unloading.
    /// </summary>
    public class ModuleLoadContext : AssemblyLoadContext
    {
        private readonly string _modulePath;
        private readonly AssemblyDependencyResolver _resolver;

        public ModuleLoadContext(string modulePath) : base(isCollectible: true)
        {
            _modulePath = modulePath;
            _resolver = new AssemblyDependencyResolver(modulePath);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            // Attempt to resolve the assembly from the module's local dependencies
            string assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                // Console.WriteLine($"Resolved assembly {assemblyName.FullName} to {assemblyPath} in module context.");
                return LoadFromAssemblyPath(assemblyPath);
            }

            // If not found in module's local dependencies, fallback to default load context
            // This allows shared framework assemblies (like System.*, Microsoft.Extensions.*) to be resolved.
            // Console.WriteLine($"Attempting to load assembly {assemblyName.FullName} in default context.");
            return null; // Returning null here means the default AssemblyLoadContext will try to load it
        }

        protected override nint LoadUnmanagedDll(string unmanagedDllName)
        {
            string libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }
            return IntPtr.Zero; // Returning IntPtr.Zero means the default AssemblyLoadContext will try to load it
        }
    }
}
