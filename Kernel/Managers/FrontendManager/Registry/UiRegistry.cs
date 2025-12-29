using Core.Backend.Messages;
using Core.Frontend.Contracts;
using Core.Frontend.Contracts.Models;
using Core.Frontend.Messages;
using Core.Log;
using Core.Pipeline;
using Core.ServiceRegistry;
using Core.Services;
using Microsoft.AspNetCore.Components.RenderTree;
using System.Collections.Concurrent;
using System.Reflection;

namespace FrontendManager.Registry
{
    /// <summary>
    /// Registry for Frontend UI components and their associated message handlers.
    /// </summary>
    /// <param name="dwauditLogger"></param>
    /// <param name="docProvider"></param>
    /// <param name="serviceProvider"></param>
    public class UiRegistry(
        IDWAuditLogger dwauditLogger,
        IDocumentationProvider docProvider,
        IServiceProvider serviceProvider) : IFrontendRegistry
    {
        // UI Elements (Visuals)
        private readonly ConcurrentDictionary<string, UiRegistrationEntry> _uiElements = new();

        // Handlers (Logic) - Key = Interface Type
        private readonly ConcurrentDictionary<Type, Type> _handlers = new();

        private readonly ConcurrentDictionary<Type, Type> _renderers = new();

        // Message Routing - Key = Route string
        private readonly ConcurrentDictionary<string, (Type MessageType, string OwnerId)> _routes = new();

        private readonly IDWAuditLogger _dwauditLogger = dwauditLogger;
        private readonly IDocumentationProvider _docProvider = docProvider;

        /// <summary>
        /// Service provider for resolving handler instances.
        /// </summary>
        public IServiceProvider ServiceProvider { get; } = serviceProvider;

        // =================================================================
        // 1. LOGIC REGISTRATION
        // =================================================================

        /// <summary>
        /// Register a handler for a specific message type using the message type's name as the command name.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <typeparam name="THandler"></typeparam>
        public void RegisterHandler<TMessage, THandler>()
            where TMessage : IMessage
            where THandler : class, IHandler<TMessage>
        {
            RegisterHandler<TMessage, THandler>(typeof(TMessage).Name);
        }

        /// <summary>
        /// Register a handler for a specific message type with a custom command name.
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
            _handlers[typeof(IHandler<TMessage>)] = handlerType;
        }

        /// <summary>
        /// Register a handler for a specific message type with a response type using the message type's name as the command name.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        /// <typeparam name="THandler"></typeparam>
        public void RegisterHandler<TMessage, TResponse, THandler>()
            where TMessage : IMessage
            where THandler : class, IHandler<TMessage, TResponse>
        {
            RegisterHandler<TMessage, TResponse, THandler>(typeof(TMessage).Name);
        }

        /// <summary>
        /// Register a handler for a specific message type with a response type and a custom command name.
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
            _handlers[typeof(IHandler<TMessage, TResponse>)] = handlerType;
        }

        private static string GetOwnerId(Type type)
        {
            return type.Assembly.GetName().Name ?? "Unknown";
        }

        // =================================================================
        // 2. VISUAL REGISTRATION
        // =================================================================

        /// <summary>
        /// Register a UI element.
        /// </summary>
        /// <param name="entry"></param>
        public void RegisterUi(UiRegistrationEntry entry)
        {
            if (_uiElements.ContainsKey(entry.Id)) return; // First-come or Priority-based logic
            _uiElements[entry.Id] = entry;
        }

        /// <summary>
        /// Register a layout component.
        /// </summary>
        /// <param name="layoutId"></param>
        /// <param name="ownerId"></param>
        /// <param name="templateId"></param>
        /// <param name="priority"></param>
        public void RegisterLayout(string layoutId, string ownerId, string templateId, UiPriority priority = UiPriority.Normal)
        {
            RegisterUi(new UiRegistrationEntry
            {
                Id = layoutId,
                OwnerId = ownerId,
                ComponentType = "Layout",
                Content = templateId,
                Priority = priority
            });
        }

        /// <summary>
        /// Register a widget component.
        /// </summary>
        /// <param name="widgetId"></param>
        /// <param name="ownerId"></param>
        /// <param name="mountPointId"></param>
        /// <param name="componentType"></param>
        /// <param name="props"></param>
        public void RegisterWidget(string widgetId, string ownerId, string mountPointId, string componentType, Dictionary<string, object>? props = null)
        {
            RegisterUi(new UiRegistrationEntry
            {
                Id = widgetId,
                OwnerId = ownerId,
                MountPointId = mountPointId,
                ComponentType = componentType,
                Properties = props ?? [],
                Priority = UiPriority.Normal
            });
        }

        /// <summary>
        /// Register a renderer for a specific FrontendManifest type.
        /// </summary>
        /// <typeparam name="TManifest"></typeparam>
        /// <typeparam name="TRenderer"></typeparam>
        void IFrontendRegistry.RegisterRenderer<TManifest, TRenderer>()
        {
            _renderers[typeof(TManifest)] = typeof(TRenderer);
        }

        /// <summary>
        /// Get the renderer type for a specific manifest type.
        /// </summary>
        /// <param name="manifestType"></param>
        /// <returns></returns>
        public Type? GetRendererType(Type manifestType)
        {
            return _renderers.TryGetValue(manifestType, out var renderer) ? renderer : null;
        }

        /// <summary>
        /// Register a zone for UI placement.
        /// </summary>
        /// <param name="zoneId"></param>
        /// <param name="ownerId"></param>
        public void RegisterZone(string zoneId, string ownerId) { }

        /// <summary>
        /// Unregister a UI element by its ID and owner ID.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="ownerId"></param>
        public void UnregisterUi(string id, string ownerId)
        {
            if (_uiElements.TryGetValue(id, out var entry) && entry.OwnerId == ownerId)
                _uiElements.TryRemove(id, out _);
        }

        // =================================================================
        // 3. RETRIEVAL
        // =================================================================

        /// <summary>
        /// Retrieve a registered UI element by its ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public UiRegistrationEntry? GetUi(string id)
        {
            _uiElements.TryGetValue(id, out var entry);
            return entry;
        }

        /// <summary>
        /// Get all registered UI elements.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<UiRegistrationEntry> GetAllUi() => _uiElements.Values;

        /// <summary>
        /// Update the state of a UI element.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="requestorId"></param>
        /// <param name="newState"></param>
        /// <exception cref="UnauthorizedAccessException"></exception>
        public void UpdateState(string id, string requestorId, UiState newState)
        {
            if (_uiElements.TryGetValue(id, out var entry))
            {
                if (entry.IsLocked && entry.OwnerId != requestorId)
                    throw new UnauthorizedAccessException("Cannot modify locked UI element.");

                if (newState.Visible.HasValue) entry.IsVisible = newState.Visible.Value;
                if (newState.Enabled.HasValue) entry.IsEnabled = newState.Enabled.Value;
                if (newState.Locked.HasValue) entry.IsLocked = newState.Locked.Value;
            }
        }

        /// <summary>
        /// Get the message type associated with a command name.
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
        /// Get the handler implementation for a given handler interface.
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <returns></returns>
        public THandler? GetHandler<THandler>() where THandler : class
        {
            var interfaceType = typeof(THandler);
            if (_handlers.TryGetValue(interfaceType, out var implementationType))
            {
                return ServiceProvider.GetService(implementationType) as THandler;
            }
            return null;
        }

        // =================================================================
        // 4. DIAGNOSTICS & CLEANUP
        // =================================================================

        /// <summary>
        /// Unregister all UI elements and handlers associated with a specific owner ID.
        /// </summary>
        /// <param name="ownerId"></param>
        public void UnregisterAll(string ownerId)
        {
            // 1. Clean UI Elements
            foreach (var key in _uiElements.Keys)
            {
                if (_uiElements.TryGetValue(key, out var ui) && ui.OwnerId == ownerId)
                {
                    _uiElements.TryRemove(key, out _);
                }
            }

            // 2. Clean Routes & Handlers
            // Note: In V2, we need a reverse map to clean _handlers efficiently by OwnerId.
            // For now, we clean Routes which effectively disables them from being called.
            var routesToRemove = _routes.Where(r => r.Value.OwnerId == ownerId).Select(r => r.Key).ToList();
            foreach (var r in routesToRemove) _routes.TryRemove(r, out _);
        }

        // =================================================================
        // 5. METADATA & SCHEMA (Developer Tools)
        // =================================================================

        /// <summary>
        /// Gets the return type name for a given message type.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<RegistryItemMetadata> GetRegistryDump()
        {
            // 1. Build Maps
            var messageToHandlerMap = new Dictionary<Type, Type>();
            var typeToRouteMap = new Dictionary<Type, string>();

            foreach (var kvp in _routes)
            {
                typeToRouteMap[kvp.Value.MessageType] = kvp.Key;

                // Simplified lookup for UI Handlers (usually just IHandler<T>)
                if (_handlers.TryGetValue(typeof(IHandler<>).MakeGenericType(kvp.Value.MessageType), out var impl))
                {
                    messageToHandlerMap[kvp.Value.MessageType] = impl;
                }
                // Handle Result types or Stream types if needed...
            }

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
                    // Refers To
                    var usesAttrs = handlerType.GetCustomAttributes<Core.Attributes.UsesAttribute>();
                    foreach (var attr in usesAttrs)
                    {
                        if (typeToRouteMap.TryGetValue(attr.MessageType, out var route))
                            metadata.RefersTo.Add(route);
                    }

                    // Referred By
                    foreach (var otherKvp in messageToHandlerMap)
                    {
                        var otherUses = otherKvp.Value.GetCustomAttributes<Core.Attributes.UsesAttribute>();
                        if (otherUses.Any(u => u.MessageType == messageType))
                        {
                            if (typeToRouteMap.TryGetValue(otherKvp.Key, out var otherRoute))
                                metadata.ReferredBy.Add(otherRoute);
                        }
                    }
                }

                yield return metadata;
            }

            // ... (Yield UI Elements) ...
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