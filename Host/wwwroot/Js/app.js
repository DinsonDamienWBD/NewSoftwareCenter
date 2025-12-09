"use strict";

// ---
//
// This script is the client-side renderer for the UIManager.
// It connects to the UIHub via SignalR and performs DOM manipulations
// based on events received from the server. It does not contain any
// business logic, only presentation logic.
//
// ---

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/uihub")
    .configureLogging(signalR.LogLevel.Information)
    .build();

// --- Element Manipulation Functions ---

const elementFactory = {
    /**
     * Creates a new element from an HTML string.
     * @param {string} htmlString The HTML content.
     * @returns {HTMLElement} The created element.
     */
    createElement: (htmlString) => {
        const template = document.createElement('template');
        template.innerHTML = htmlString.trim();
        return template.content.firstChild;
    },

    /**
     * Adds an element to the DOM.
     * @param {object} elementData - The element data from the server.
     * @param {string} elementData.elementId - The ID of the element to add.
     * @param {string} elementData.parentId - The ID of the parent element.
     * @param {string} elementData.htmlContent - The inner HTML of the new element.
     * @param {string} [elementData.cssContent] - Optional CSS to inject.
     */
    addElement: (elementData) => {
        const parent = document.getElementById(elementData.parentId);
        if (!parent) {
            console.error(`Parent element with ID '${elementData.parentId}' not found.`);
            return;
        }

        // Avoid adding if it already exists
        if (document.getElementById(elementData.elementId)) {
            console.warn(`Element with ID '${elementData.elementId}' already exists.`);
            return;
        }

        const newElement = elementFactory.createElement(elementData.htmlContent);
        newElement.id = elementData.elementId;

        // Handle specific logic, e.g., for navigation
        if (parent.id === 'nav-zone') {
            newElement.addEventListener('click', () => handleNavClick(newElement.id));
        }
        
        parent.appendChild(newElement);

        if (elementData.cssContent) {
            const style = document.createElement('style');
            style.textContent = elementData.cssContent;
            document.head.appendChild(style);
        }
    },

    /**
     * Updates an existing element.
     * @param {object} elementData - The element data from the server.
     * @param {string} elementData.elementId - The ID of the element to update.
     * @param {string} [elementData.htmlContent] - The new inner HTML.
     * @param {object} [elementData.attributes] - An object of attributes to set (e.g., {'class': 'new-class', 'data-value': '123'}).
     */
    updateElement: (elementData) => {
        const element = document.getElementById(elementData.elementId);
        if (!element) {
            console.error(`Element with ID '${elementData.elementId}' not found for update.`);
            return;
        }

        if (elementData.htmlContent) {
            element.innerHTML = elementData.htmlContent;
        }

        if (elementData.attributes) {
            for (const [key, value] of Object.entries(elementData.attributes)) {
                if (value === null) {
                    element.removeAttribute(key);
                } else {
                    element.setAttribute(key, value);
                }
            }
        }
    },

    /**
     * Removes an element from the DOM.
     * @param {object} elementData - The element data from the server.
     * @param {string} elementData.elementId - The ID of the element to remove.
     */
    removeElement: (elementData) => {
        const element = document.getElementById(elementData.elementId);
        if (element) {
            element.parentNode.removeChild(element);
        } else {
            console.warn(`Element with ID '${elementData.elementId}' not found for removal.`);
        }
    }
};

// --- Event Handlers ---

/**
 * Handles clicks on navigation buttons.
 * @param {string} navButtonId - The ID of the clicked nav button.
 */
function handleNavClick(navButtonId) {
    // Deactivate all content containers and nav buttons
    document.querySelectorAll('#content-zone .content-container').forEach(c => c.classList.remove('active'));
    document.querySelectorAll('#nav-zone .nav-button').forEach(b => b.classList.remove('active'));
    
    const navButton = document.getElementById(navButtonId);
    if (!navButton) {
        console.error(`Nav button with ID '${navButtonId}' not found.`);
        return;
    }

    // Activate the target content container and nav button
    const contentId = navButton.dataset.targetContainer; // Use the data attribute
    const contentContainer = document.getElementById(contentId);

    if (contentContainer) {
        contentContainer.classList.add('active');
    } else {
        console.error(`Content container with ID '${contentId}' not found.`);
    }

    navButton.classList.add('active');
}


// --- SignalR Event Listeners ---

connection.on("ElementAdded", (elementData) => {
    console.log("ElementAdded:", elementData);
    elementFactory.addElement(elementData);
});

connection.on("ElementUpdated", (elementData) => {
    console.log("ElementUpdated:", elementData);
    elementFactory.updateElement(elementData);
});

connection.on("ElementRemoved", (elementData) => {
    console.log("ElementRemoved:", elementData);
    elementFactory.removeElement(elementData);
});

// --- Connection Start ---

async function start() {
    try {
        await connection.start();
        console.log("SignalR Connected.");
    } catch (err) {
        console.log(err);
        setTimeout(start, 5000);
    }
};

connection.onclose(async () => {
    await start();
});

// Start the connection
start();
