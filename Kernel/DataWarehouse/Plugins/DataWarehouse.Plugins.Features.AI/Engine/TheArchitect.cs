using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Runtime.Loader;

namespace DataWarehouse.Plugins.Features.AI.Engine
{
    /// <summary>
    /// GOD TIER ENGINE: The Architect.
    /// Uses Roslyn to dynamically compile and hot-swap code at runtime.
    /// Allows the system to evolve its own behavior.
    /// </summary>
    public class TheArchitect(IKernelContext context, PluginRegistry registry)
    {
        private readonly IKernelContext _context = context;
        private readonly PluginRegistry _registry = registry;

        /// <summary>
        /// Compiles and loads a C# source string as a new Plugin.
        /// </summary>
        /// <param name="csharpCode">The raw C# source code.</param>
        /// <returns>The ID of the loaded plugin.</returns>
        public string Evolve(string csharpCode)
        {
            _context.LogInfo("[Architect] Initiating Self-Evolution...");

            // 1. Create Syntax Tree
            var syntaxTree = CSharpSyntaxTree.ParseText(csharpCode);

            // 2. Add References (Kernel, SDK, System)
            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IKernelContext).Assembly.Location), // SDK
                MetadataReference.CreateFromFile(typeof(PluginRegistry).Assembly.Location),  // Kernel
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)
            };

            // 3. Compile
            var compilation = CSharpCompilation.Create(
                $"Evolution_{Guid.NewGuid():N}",
                [syntaxTree],
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            if (!result.Success)
            {
                var failures = result.Diagnostics.Where(diagnostic =>
                    diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

                foreach (var diagnostic in failures)
                {
                    _context.LogError($"[Architect] Compilation Failure: {diagnostic.Id}: {diagnostic.GetMessage()}");
                }
                throw new InvalidOperationException("Evolution Failed.");
            }

            // 4. Load Assembly
            ms.Seek(0, SeekOrigin.Begin);
            var assembly = AssemblyLoadContext.Default.LoadFromStream(ms);

            // 5. Instantiate Plugin
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IPlugin).IsAssignableFrom(type) && !type.IsInterface)
                {
                    var plugin = (IPlugin)Activator.CreateInstance(type)!;
                    plugin.Initialize(_context);
                    _registry.Register(plugin);
                    _context.LogInfo($"[Architect] Successfully Evolved: {plugin.Name} v{plugin.Version}");
                    return plugin.Id;
                }
            }

            throw new InvalidOperationException("Code compiled but no IPlugin found.");
        }
    }
}