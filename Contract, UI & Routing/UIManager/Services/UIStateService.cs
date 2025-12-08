using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using SoftwareCenter.Core.UI;

namespace SoftwareCenter.UIManager.Services
{
    /// <summary>
    /// Manages the state of all UI elements in the application.
    /// This service is the single source of truth for the UI's structure and properties.
    /// </summary>
    public class UIStateService
    {
        private readonly ConcurrentDictionary<Guid, UIElement> _elements = new ConcurrentDictionary<Guid, UIElement>();

        /// <summary>
        /// Event raised when the UI state changes.
        /// </summary>
        public event Action UIStateChanged;

        /// <summary>
        /// Adds a new UI element to the state.
        /// </summary>
        /// <param name="element">The element to add.</param>
        /// <returns>True if the element was added, false if an element with the same ID already exists.</returns>
        public bool TryAddElement(UIElement element)
        {
            if (_elements.TryAdd(element.Id, element))
            {
                UIStateChanged?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets a UI element by its ID.
        /// </summary>
        /// <param name="id">The ID of the element to retrieve.</param>
        /// <param name="element">The output element, if found.</param>
        /// <returns>True if an element with the given ID was found, otherwise false.</returns>
        public bool TryGetElement(Guid id, out UIElement element)
        {
            return _elements.TryGetValue(id, out element);
        }

        /// <summary>
        /// Retrieves a UI element by its ID.
        /// </summary>
        /// <param name="id">The ID of the element to retrieve.</param>
        /// <returns>The UIElement if found, otherwise null.</returns>
        public UIElement GetElement(Guid id)
        {
            _elements.TryGetValue(id, out var element);
            return element;
        }

        /// <summary>
        /// Gets a snapshot of all current UI elements.
        /// </summary>
        /// <returns>A thread-safe collection of all UI elements.</returns>
        public ICollection<UIElement> GetAllElements()
        {
            return _elements.Values;
        }
    }
}
