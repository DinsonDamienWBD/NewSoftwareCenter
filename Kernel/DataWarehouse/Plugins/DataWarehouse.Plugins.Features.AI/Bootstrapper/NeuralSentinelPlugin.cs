using DataWarehouse.Plugins.Features.AI.Modules;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Governance;
using System.Reflection;
using System.Text.Json;

namespace DataWarehouse.Plugins.Features.AI.Bootstrapper
{
    public class NeuralSentinelPlugin : IFeaturePlugin, INeuralSentinel
    {
        public string Id => "neural-sentinel-core";
        public string Name => "Neural Sentinel Core (Dynamic)";
        public string Version => "9.2.0";

        private IKernelContext? _context;
        private readonly List<ISentinelModule> _modules = new();

        // The Live Registry: Maps "CommandName" -> Executable Delegate & Metadata
        private readonly Dictionary<string, SkillDefinition> _skillRegistry = new();

        public void Initialize(IKernelContext context)
        {
            _context = context;

            // 1. Load Assemblies (Same as before)
            LoadModuleAssemblies(context.RootPath);

            // 2. REFLECTION DISCOVERY (The V9.2 Upgrade)
            // Scans all loaded modules for [SentinelSkill] methods
            DiscoverSkills();

            context.LogInfo($"[{Id}] Brain Online. Indexing complete: {_modules.Count} Modules, {_skillRegistry.Count} AI Skills.");

            // Debug: Print available skills to log
            foreach (var skill in _skillRegistry.Values)
            {
                context.LogDebug($"[Skill] {skill.Name} ({skill.Category}): {skill.Description}");
            }
        }

        private void LoadModuleAssemblies(string rootPath)
        {
            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (assemblyLocation == null) return;

            var dlls = Directory.GetFiles(assemblyLocation, "DataWarehouse.AI.*.dll", SearchOption.TopDirectoryOnly)
                       .Concat(Directory.GetFiles(assemblyLocation, "DataWarehouse.Plugins.Neural.dll"));

            foreach (var dll in dlls)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dll);
                    var moduleTypes = assembly.GetTypes()
                        .Where(t => typeof(ISentinelModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                    foreach (var type in moduleTypes)
                    {
                        if (Activator.CreateInstance(type) is ISentinelModule module)
                        {
                            if (!_modules.Any(m => m.ModuleId == module.ModuleId))
                                _modules.Add(module);
                        }
                    }
                }
                catch (Exception ex) { _context?.LogWarning($"Failed to load {Path.GetFileName(dll)}: {ex.Message}"); }
            }
        }

        private void DiscoverSkills()
        {
            _skillRegistry.Clear();

            foreach (var module in _modules)
            {
                var methods = module.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);

                foreach (var method in methods)
                {
                    var attr = method.GetCustomAttribute<SentinelSkillAttribute>();
                    if (attr == null) continue;

                    // Build Parameter Metadata
                    var parameters = method.GetParameters().Select(p =>
                    {
                        var pAttr = p.GetCustomAttribute<SentinelParameterAttribute>();
                        return new SkillParameter
                        {
                            Name = p.Name ?? "arg",
                            Type = p.ParameterType.Name,
                            Description = pAttr?.Description ?? "No description provided.",
                            Example = pAttr?.Example ?? "",
                            IsRequired = pAttr?.IsRequired ?? !p.HasDefaultValue
                        };
                    }).ToList();

                    var def = new SkillDefinition
                    {
                        Name = attr.Name,
                        Description = attr.Description,
                        Category = attr.Category,
                        HostModule = module,
                        MethodInfo = method,
                        Parameters = parameters
                    };

                    _skillRegistry[attr.Name] = def;
                }
            }
        }

        public Task StartAsync(CancellationToken ct)
        {
            _ = Task.Run(() => new SentinelDaemon(_context!, this).RunAsync(ct), ct);
            return Task.CompletedTask;
        }

        public Task StopAsync() => Task.CompletedTask;

        public async Task<GovernanceJudgment> EvaluateAsync(SentinelContext context)
        {
            var finalJudgment = new GovernanceJudgment { InterventionRequired = false };

            foreach (var module in _modules)
            {
                // Future: Check if module is enabled via _skills configuration
                var partial = await module.AnalyzeAsync(context, _context!);
                if (partial.InterventionRequired)
                {
                    finalJudgment.InterventionRequired = true;
                    if (partial.BlockOperation) finalJudgment.BlockOperation = true;
                    if (partial.EnforcePipeline != null) finalJudgment.EnforcePipeline = partial.EnforcePipeline;
                    finalJudgment.AddTags.AddRange(partial.AddTags);
                    if (partial.Alert != null) finalJudgment.Alert = partial.Alert; // Simple override for now
                }
            }
            return finalJudgment;
        }

        // --- Helper DTOs for JSON ---
        public class AgentCommandDefinition
        {
            public string CommandName { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string TargetModuleId { get; set; } = string.Empty; // Maps to ISentinelModule.ModuleId
            public Dictionary<string, string> Parameters { get; set; } = new();
        }

        // --- DTOs for the Dynamic Registry ---

        public class SkillDefinition
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public List<SkillParameter> Parameters { get; set; } = new();

            // Execution context
            public ISentinelModule HostModule { get; set; } = default!;
            public MethodInfo MethodInfo { get; set; } = default!;
        }

        public class SkillParameter
        {
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Example { get; set; } = string.Empty;
            public bool IsRequired { get; set; }
        }
    }
}