// --- Globals ---
const outputPre = document.getElementById('output-pre');
const uiRoot = document.getElementById('ui-root');
let mainPanelId = null;

// --- SignalR Setup ---
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/uihub")
    .build();

connection.on("ReceiveUIUpdate", (uiState) => {
    console.log("UI Update received:", uiState);
    renderUi(uiState);
});

async function startSignalR() {
    try {
        await connection.start();
        console.log("SignalR Connected.");
        // Request initial UI state immediately after connecting
        await getUiState().then(renderUi);
    } catch (err) {
        console.error(err);
        setTimeout(startSignalR, 5000); // Retry connection
    }
}

connection.onclose(async () => {
    console.log("SignalR Disconnected. Attempting to reconnect...");
    await startSignalR();
});

// --- API Communication ---
async function dispatchCommand(commandName, payload = {}) {
    try {
        const response = await fetch(`/api/dispatch/${commandName}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload),
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Server error (${response.status}): ${errorText}`);
        }

        const contentType = response.headers.get("content-type");
        if (contentType && contentType.indexOf("application/json") !== -1) {
            return await response.json();
        } else {
            return null; // No content or non-JSON content
        }
    } catch (err) {
        console.error(`Error dispatching ${commandName}:`, err);
        outputPre.textContent = err.message;
        return null;
    }
}

async function getUiState() {
    try {
        const response = await fetch('/api/ui_state');
        if (!response.ok) throw new Error('Failed to fetch UI state');
        return await response.json();
    } catch (err) {
        console.error('Error getting UI state:', err);
        return [];
    }
}

// --- UI Rendering ---
function renderUi(elements) {
    uiRoot.innerHTML = ''; // Clear previous state
    const elementMap = new Map(elements.map(el => [el.id, el]));
    const domMap = new Map();

    // First pass: create all DOM elements
    for (const el of elements) {
        let domEl;
        switch (el.type) {
            case 0: // Panel
                domEl = document.createElement('div');
                domEl.className = 'element-panel';
                break;
            case 1: // Button
                domEl = document.createElement('button');
                domEl.className = 'element-button';
                domEl.textContent = el.properties.Text || 'Button';
                break;
            default:
                domEl = document.createElement('div');
                domEl.textContent = `Unknown Element Type: ${el.type}`;
                break;
        }
        if (domEl) {
            domEl.id = el.id;
            domMap.set(el.id, domEl);
        }
    }

    // Second pass: append elements to their parents
    for (const el of elements) {
        const domEl = domMap.get(el.id);
        if (!domEl) continue;

        if (el.parentId && domMap.has(el.parentId)) {
            domMap.get(el.parentId).appendChild(domEl);
        } else {
            uiRoot.appendChild(domEl); // Append to root if no parent
        }
    }
}

// --- Event Listeners ---
document.getElementById('btnCreatePanel').addEventListener('click', async () => {
    const result = await dispatchCommand('CreateElement', { elementType: 0, initialProperties: { 'Name': 'MainPanel', 'Text': 'Main UI Panel' } });
    if (result) {
        mainPanelId = result;
        outputPre.textContent = `Main panel created with ID: ${mainPanelId}`;
        // UI will auto-update via SignalR, no need to call renderUi() here
    }
});

document.getElementById('btnCreateButton').addEventListener('click', async () => {
    if (!mainPanelId) {
        outputPre.textContent = 'Please create the main panel first.';
        return;
    }
    const result = await dispatchCommand('CreateElement', { elementType: 1, parentId: mainPanelId, initialProperties: { 'Text': 'Click Me!' } });
    if (result) {
        outputPre.textContent = `Button created with ID: ${result}`;
        // UI will auto-update via SignalR
    }
});

document.getElementById('btnGetManifest').addEventListener('click', async () => {
    const result = await dispatchCommand('GetRegistryManifest');
    if (result) {
        outputPre.textContent = JSON.stringify(result, null, 2);
    }
});

// --- Initial Load ---
// Load SignalR client library dynamically
const script = document.createElement('script');
script.src = "https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js";
script.onload = startSignalR;
document.head.appendChild(script);
