using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// An in-memory implementation of <see cref="IServiceRoutingRegistry"/>
    /// that stores handler registrations with priority information.
    /// </summary>
    public class ServiceRoutingRegistry : IServiceRoutingRegistry
    {
        // Stores a list of handlers for each contract type, sorted by priority (highest first)
        private readonly ConcurrentDictionary<Type, SortedSet<HandlerRegistration>> _registrations = new ConcurrentDictionary<Type, SortedSet<HandlerRegistration>>();

        public void RegisterHandler(Type contractType, Type handlerType, Type handlerInterfaceType, int priority, string owningModuleId)
        {
            var newRegistration = new HandlerRegistration(contractType, handlerType, handlerInterfaceType, priority, owningModuleId);
            
            _registrations.AddOrUpdate(
                contractType,
                (key) => new SortedSet<HandlerRegistration>(new HandlerRegistrationComparer()) { newRegistration },
                (key, existingSet) => {
                    // Check if an existing registration from the same module and type exists, and replace it
                    // This handles scenarios where a module might re-register a handler, or for clarity.
                    existingSet.RemoveWhere(r => r.OwningModuleId == owningModuleId && r.HandlerType == handlerType);
                    existingSet.Add(newRegistration);
                    return existingSet;
                });
        }

        public HandlerRegistration GetHighestPriorityHandler(Type contractType)
        {
            if (_registrations.TryGetValue(contractType, out var handlers))
            {
                return handlers.FirstOrDefault(); // First item is highest priority due to SortedSet ordering
            }
            return null;
        }

        public IEnumerable<HandlerRegistration> GetAllHandlers(Type contractType)
        {
            if (_registrations.TryGetValue(contractType, out var handlers))
            {
                return handlers;
            }
            return Enumerable.Empty<HandlerRegistration>();
        }

        /// <summary>
        /// Comparer for HandlerRegistration to ensure sorting by priority (highest first) and then by module ID for tie-breaking.
        /// </summary>
        private class HandlerRegistrationComparer : IComparer<HandlerRegistration>
        {
            public int Compare(HandlerRegistration x, HandlerRegistration y)
            {
                // Sort by priority in descending order
                int result = y.Priority.CompareTo(x.Priority);
                if (result == 0)
                {
                    // If priorities are equal, use module ID as a tie-breaker (for consistent ordering)
                    result = string.Compare(x.OwningModuleId, y.OwningModuleId, StringComparison.Ordinal);
                }
                return result;
            }
        }
    }
}
