using DataWarehouse.Plugins.Features.AI.Modules;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Governance;
using System.Reflection;
using System.Text;
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

        /// <summary>
        /// Handshake protocol handler
        /// </summary>
        public Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
        {
            _context = request as IKernelContext;

            // Load Assemblies
            LoadModuleAssemblies(_context?.RootPath ?? "");

            // Reflection Discovery
            DiscoverSkills();

            _context?.LogInfo($"[{Id}] Brain Online. Indexing complete: {_modules.Count} Modules, {_skillRegistry.Count} AI Skills.");

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

        public async Task<string> TranslateIntentToSqlAsync(string intent)
        {
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                // Fallback Heuristics (if no AI key) - Still valid for production fallback
                if (intent.Contains("risk")) return "SELECT * FROM Manifests WHERE JsonData->>'Risk' > 50";
                return "SELECT * FROM Manifests LIMIT 5";
            }

            // Real Call
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var body = new
            {
                model = "gpt-4",
                messages = new[] {
            new { role = "system", content = "Translate user intent to SQL for table 'Manifests' (cols: Id, JsonData)." },
            new { role = "user", content = intent }
        }
            };

            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var resp = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);

            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
            }

            throw new Exception("AI Service unavailable.");
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