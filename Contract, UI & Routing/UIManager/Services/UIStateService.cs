using SoftwareCenter.Core.Events;
using SoftwareCenter.Core.UI;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System;

namespace SoftwareCenter.UIManager.Services
{
    /// <summary>
    /// Manages the in-memory state of all UI elements. This service is the single source of truth for the UI's structure.
    /// It handles the registration, retrieval, and removal of UI elements, and manages priority-based overrides.
    /// NOTE: This file should be renamed to UIManagerService.cs
    /// </summary>
    public class UIStateService
    {
        private readonly IEventBus _eventBus;
        private readonly ConcurrentDictionary<string, UIElement> _elements = new();
        private readonly ConcurrentDictionary<string, SortedSet<UIElement>> _elementSlots = new();

        public UIStateService(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        /// <summary>
        /// Creates a new UI element, stores it, and publishes an event.
        /// </summary>
        public UIElement CreateElement(string ownerId, ElementType elementType, string parentId, string htmlContent, string? cssContent = null, string? jsContent = null, int priority = 0, string? slotName = null, Dictionary<string, object>? properties = null, string? id = null)
        {
            var element = new UIElement
            {
                Id = id ?? Guid.NewGuid().ToString("N"),
                OwnerModuleId = ownerId,
                ElementType = elementType,
                ParentId = parentId,
                Priority = priority,
                SlotName = slotName,
                Properties = properties ?? new Dictionary<string, object>()
            };

            if (!_elements.TryAdd(element.Id, element))
            {
                // Handle error: could not add element
                return null;
            }

            if (!string.IsNullOrEmpty(element.SlotName))
            {
                var slot = _elementSlots.GetOrAdd(element.SlotName, _ => new SortedSet<UIElement>(new ElementPriorityComparer()));
                slot.Add(element);
            }

            _eventBus.Publish(new UIElementRegisteredEvent(element, htmlContent, cssContent, jsContent));

            return element;
        }

        public UIElement GetElement(string id)
        {
            _elements.TryGetValue(id, out var element);
            return element;
        }

        /// <summary>
        /// Removes an element and publishes an event.
        /// </summary>
        public bool DeleteElement(string id)
        {
            if (!_elements.TryRemove(id, out var element))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(element.SlotName) && _elementSlots.TryGetValue(element.SlotName, out var slot))
            {
                slot.Remove(element);
            }

            _eventBus.Publish(new UIElementUnregisteredEvent(id));
            return true;
        }

        /// <summary>
        /// Gets the highest-priority, active element for a given slot name.
        /// </summary>
        public UIElement GetActiveElementForSlot(string slotName)
        {
            if (_elementSlots.TryGetValue(slotName, out var slot))
            {
                return slot.FirstOrDefault();
            }
            return null;
        }

        public IEnumerable<UIElement> GetAllElements()
        {
            return _elements.Values;
        }

        /// <summary>
        /// Updates an element's properties and publishes an event.
        /// </summary>
        public bool UpdateElement(string id, Dictionary<string, object> propertiesToUpdate)
        {
            if (!_elements.TryGetValue(id, out var element))
            {
                return false;
            }

            foreach (var prop in propertiesToUpdate)
            {
                element.Properties[prop.Key] = prop.Value;
            }

            _eventBus.Publish(new UIElementUpdatedEvent(id, propertiesToUpdate));
            return true;
        }

        /// <summary>
        /// Sets the access control for a UI element and publishes an event.
        /// </summary>
        public bool SetAccessControl(string id, UIAccessControl newAccessControl)
        {
            if (!_elements.TryGetValue(id, out var element))
            {
                return false;
            }

            element.AccessControl = newAccessControl;

            _eventBus.Publish(new UIOwnershipChangedEvent(id, newAccessControl));
            return true;
        }
    }

    /// <summary>
    /// Comparer to sort UIElements by priority, with higher priority values coming first.
    /// </summary>
    file class ElementPriorityComparer : IComparer<UIElement>
    {
        public int Compare(UIElement x, UIElement y)
        {
            if (x == null || y == null) return 0;

            int priorityComparison = y.Priority.CompareTo(x.Priority);
            if (priorityComparison != 0)
            {
                return priorityComparison;
            }
            
            return string.Compare(x.Id, y.Id, StringComparison.Ordinal);
        }
    }
}
