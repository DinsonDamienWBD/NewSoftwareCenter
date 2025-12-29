using Core.Backend.Contracts;
using Core.Backend.Contracts.Models;
using Core.Backend.Messages;
using Core.Log;
using Core.Pipeline;
using Core.ServiceRegistry;
using Core.Services;
using System.Collections.Concurrent;
using System.Reflection;

namespace BackendManager.Registry
{
    /// <summary>
    /// Service Registry for Backend Handlers and Services.
    /// </summary>
    /// <param name="dwauditLogger"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="docProvider"></param>
    public class ServiceRegistry(IDWAuditLogger dwauditLogger, IServiceProvider serviceProvider, IDocumentationProvider docProvider) : IBackendRegistry
    {
        // Key = Interface Type (e.g., IHandler<Cmd>), Value = List of Implementations
        private readonly ConcurrentDictionary<Type, List<ServiceRegistrationEntry>> _services = new();

        // Key = Route (lowercased), Value = Metadata about the route
        private readonly ConcurrentDictionary<string, (Type MessageType, string OwnerId)> _routes = new();

        private readonly IDWAuditLogger _dwauditLogger = dwauditLogger;

        /// <summary>
        /// Service Provider for resolving dependencies.
        /// </summary>
        public IServiceProvider ServiceProvider { get; } = serviceProvider;
        private readonly IDocumentationProvider _docProvider = docProvider;

        // =================================================================
        // 1. REGISTRATION LOGIC
        // =================================================================

        /// <summary>
        /// Register a Handler for a specific Message Type.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <typeparam name="THandler"></typeparam>
        public void RegisterHandler<TMessage, THandler>()
            where TMessage : IMessage
            where THandler : class, IHandler<TMessage>
            => RegisterHandler<TMessage, THandler>(typeof(TMessage).Name);

        /// <summary>
        /// Register a Handler for a specific Message Type with a custom command name.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="commandName"></param>
        public void RegisterHandler<TMessage, THandler>(string commandName)
            where TMessage : IMessage
            where THandler : class, IHandler<TMessage>
        {
            var handlerType = typeof(THandler);
            var ownerId = GetOwnerId(handlerType);
            _routes[commandName.ToLower()] = (typeof(TMessage), ownerId);
            AddService(typeof(IHandler<TMessage>), handlerType, ownerId);
        }

        /// <summary>
        /// Register a Handler for a specific Message Type with a Response Type.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        /// <typeparam name="THandler"></typeparam>
        public void RegisterHandler<TMessage, TResponse, THandler>()
            where TMessage : IMessage
            where THandler : class, IHandler<TMessage, TResponse>
            => RegisterHandler<TMessage, TResponse, THandler>(typeof(TMessage).Name);

        /// <summary>
        /// Register a Handler for a specific Message Type with a Response Type and custom command name.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="commandName"></param>
        public void RegisterHandler<TMessage, TResponse, THandler>(string commandName)
            where TMessage : IMessage
            where THandler : class, IHandler<TMessage, TResponse>
        {
            var handlerType = typeof(THandler);
            var ownerId = GetOwnerId(handlerType);
            _routes[commandName.ToLower()] = (typeof(TMessage), ownerId);
            AddService(typeof(IHandler<TMessage, TResponse>), handlerType, ownerId);
        }

        /// <summary>
        /// Register a service instance with a specific name and priority.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <param name="implementation"></param>
        /// <param name="name"></param>
        /// <param name="priority"></param>
        public void Register<TService>(TService implementation, string name, ServicePriority priority) where TService : class
        {
            // For instances passed directly (Singleton), we wrap them.
            // Note: In a real DI scenario, we'd usually add to IServiceCollection, 
            // but for runtime plugins, we track them here or use a child scope.
            // For V2, we will treat this as a metadata registration.
        }

        // =================================================================
        // 2. INTERNAL HELPERS
        // =================================================================

        private void AddService(Type interfaceType, Type handlerType, string ownerId)
        {
            var entry = new ServiceRegistrationEntry
            {
                InterfaceType = interfaceType,
                Implementation = handlerType, // Storing Type for DI resolution
                OwnerId = ownerId,
                Priority = ServicePriority.Normal,
                Description = _docProvider.GetTypeSummary(handlerType),
                IsInternal = false,
                IsSealed = false
            };

            _services.AddOrUpdate(interfaceType,
                // Add New
                _ => [entry],
                // Update Existing
                (_, list) => { list.Add(entry); return list; });
        }

        private static string GetOwnerId(Type type) => type.Assembly.GetName().Name ?? "Unknown";

        // =================================================================
        // 3. RESOLUTION
        // =================================================================

        /// <summary>
        /// Get the Message Type associated with a given command name.
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        public Type? GetMessageType(string typeName)
        {
            if (_routes.TryGetValue(typeName.ToLower(), out var data))
                return data.MessageType;
            return null;
        }

        /// <summary>
        /// Get a single service implementation of the specified type.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <returns></returns>
        public TService? GetService<TService>() where TService : class
        {
            return GetServices<TService>().FirstOrDefault();
        }

        /// <summary>
        /// Get all service implementations of the specified type.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <returns></returns>
        public IEnumerable<TService> GetServices<TService>() where TService : class
        {
            var interfaceType = typeof(TService);
            if (_services.TryGetValue(interfaceType, out var entries))
            {
                foreach (var entry in entries.OrderByDescending(e => e.Priority))
                {
                    if (entry.Implementation is Type serviceType)
                    {
                        var instance = ServiceProvider.GetService(serviceType);
                        if (instance is TService typedInstance)
                            yield return typedInstance;
                    }
                }
            }
        }

        // =================================================================
        // 4. MANAGEMENT (Unloading)
        // =================================================================

        /// <summary>
        /// Temporarily unregister a specific service implementation.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <param name="implementation"></param>
        public void Unregister<TService>(TService implementation) where TService : class
        {
            // For transient services registered by Type, we can't unregister a specific instance easily.
            // This method is generally used for Singleton instances passed directly.
            // For V2, we focus on UnregisterAll(ownerId) as the primary mechanism for Plugins.
        }

        /// <summary>
        /// Unregister all services and routes associated with the given ownerId.
        /// </summary>
        /// <param name="ownerId"></param>
        public void UnregisterAll(string ownerId)
        {
            // 1. Clean Services
            foreach (var key in _services.Keys)
            {
                if (_services.TryGetValue(key, out var list))
                {
                    list.RemoveAll(x => x.OwnerId == ownerId);
                    if (list.Count == 0) _services.TryRemove(key, out _);
                }
            }

            // 2. Clean Routes
            var routesToRemove = _routes.Where(kvp => kvp.Value.OwnerId == ownerId).Select(k => k.Key).ToList();
            foreach (var route in routesToRemove)
            {
                _routes.TryRemove(route, out _);
            }
        }

        // =================================================================
        // 5. METADATA & SCHEMA (Developer Tools)
        // =================================================================

        /// <summary>
        /// Get a dump of all registered routes with metadata.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<RegistryItemMetadata> GetRegistryDump()
        {
            // 1. Snapshot all registrations to build the graph
            // We need a map of "Message Type" -> "Handler Type" to do the reverse lookup
            var messageToHandlerMap = new Dictionary<Type, Type>();

            // We also need "Message Type" -> "Route" to create the hyperlinks
            var typeToRouteMap = new Dictionary<Type, string>();

            foreach (var kvp in _routes)
            {
                typeToRouteMap[kvp.Value.MessageType] = kvp.Key;

                // Find the implementation for this message
                var interfaceType = typeof(IHandler<>).MakeGenericType(kvp.Value.MessageType);
                // Try finding generic with result too
                if (!_services.ContainsKey(interfaceType))
                {
                    // Scan for IHandler<T, R>
                    var key = _services.Keys.FirstOrDefault(k => k.IsGenericType && k.GetGenericArguments()[0] == kvp.Value.MessageType);
                    if (key != null) interfaceType = key;
                }

                if (_services.TryGetValue(interfaceType, out var entries))
                {
                    var impl = entries.FirstOrDefault()?.Implementation as Type;
                    if (impl != null) messageToHandlerMap[kvp.Value.MessageType] = impl;
                }
            }

            // 2. Generate Metadata
            foreach (var kvp in _routes)
            {
                var messageType = kvp.Value.MessageType;
                var handlerType = messageToHandlerMap.GetValueOrDefault(messageType);

                var metadata = new RegistryItemMetadata
                {
                    Route = kvp.Key,
                    Type = GetReturnType(messageType),
                    Description = _docProvider.GetTypeSummary(messageType),
                    OwnerId = kvp.Value.OwnerId,
                    PayloadSchema = GenerateRichSchema(messageType)
                };

                // --- GRAPH LOGIC ---

                if (handlerType != null)
                {
                    // A. Refers To (Forward)
                    // Look for [Uses(typeof(OtherMessage))] on this Handler
                    var usesAttrs = handlerType.GetCustomAttributes<Core.Attributes.UsesAttribute>();
                    foreach (var attr in usesAttrs)
                    {
                        if (typeToRouteMap.TryGetValue(attr.MessageType, out var route))
                        {
                            metadata.RefersTo.Add(route);
                        }
                    }

                    // B. Referred By (Reverse)
                    // Look at ALL other handlers to see if they Use ME
                    foreach (var otherKvp in messageToHandlerMap)
                    {
                        var otherHandler = otherKvp.Value;
                        var otherRoute = typeToRouteMap.GetValueOrDefault(otherKvp.Key);

                        if (otherRoute == null) continue;

                        var otherUses = otherHandler.GetCustomAttributes<Core.Attributes.UsesAttribute>();
                        if (otherUses.Any(u => u.MessageType == messageType))
                        {
                            metadata.ReferredBy.Add(otherRoute);
                        }
                    }
                }

                yield return metadata;
            }
        }

        private static string GetReturnType(Type messageType)
        {
            var queryInterface = messageType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>));
            if (queryInterface != null) return $"Query<{GetFriendlyName(queryInterface.GetGenericArguments()[0])}>";

            var cmdInterface = messageType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));
            if (cmdInterface != null) return $"Command<{GetFriendlyName(cmdInterface.GetGenericArguments()[0])}>";

            if (typeof(IJob).IsAssignableFrom(messageType)) return "Job";

            return "Command (Void)";
        }

        private static string GetFriendlyName(Type type)
        {
            if (!type.IsGenericType) return type.Name;
            var name = type.Name.Split('`')[0];
            var args = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyName));
            return $"{name}<{args}>";
        }

        /// <summary>
        /// Generates a recursive, rich schema description for the Frontend UI.
        /// </summary>
        private SchemaDefinition GenerateRichSchema(Type type, int depth = 0)
        {
            // 1. Circuit Breaker (prevent infinite recursion)
            if (depth > 5) return new SchemaDefinition { TypeName = "recursion_limit" };

            // 2. Handle Nullables (e.g. int?) -> treat as int
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            // 3. Primitives
            if (underlying == typeof(string) || underlying == typeof(Guid) || underlying == typeof(DateTime))
                return new SchemaDefinition { TypeName = "string", DefaultValue = "" };

            if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(double))
                return new SchemaDefinition { TypeName = "number", DefaultValue = 0 };

            if (underlying == typeof(bool))
                return new SchemaDefinition { TypeName = "bool", DefaultValue = false };

            // 4. Enums (The Dropdown Logic)
            if (underlying.IsEnum)
            {
                return new SchemaDefinition
                {
                    TypeName = "enum",
                    EnumValues = [.. Enum.GetNames(underlying)],
                    DefaultValue = Enum.GetNames(underlying).FirstOrDefault()
                };
            }

            // 5. Collections (Arrays/Lists)
            if (underlying != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(underlying))
            {
                // Try to find the inner type (e.g., List<T> -> T)
                var itemType = underlying.IsArray
                    ? underlying.GetElementType()
                    : underlying.GetGenericArguments().FirstOrDefault();

                return new SchemaDefinition
                {
                    TypeName = "array",
                    ArrayItemType = GenerateRichSchema(itemType ?? typeof(object), depth + 1),
                    DefaultValue = Array.Empty<object>()
                };
            }

            // 6. Complex Objects (The Recursive Step)
            var schema = new SchemaDefinition
            {
                TypeName = "object",
                Properties = []
            };

            foreach (var prop in underlying.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.Name == "MessageId" || prop.Name == "Timestamp") continue; // Noise reduction

                var propSchema = GenerateRichSchema(prop.PropertyType, depth + 1);

                // INJECT XML DOCS HERE
                propSchema.Description = _docProvider.GetPropertySummary(prop);

                schema.Properties[prop.Name] = propSchema;
            }

            return schema;
        }
    }
}