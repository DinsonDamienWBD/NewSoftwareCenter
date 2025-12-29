// SoftwareCenter Host Client Runtime
window.ModuleRegistry = {};

// SignalR Setup
// Note: In V1 we assume signalr.js is loaded via CDN or static file
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/frontendHub")
    .withAutomaticReconnect()
    .build();

connection.on("InjectFragment", (targetGuid, mountPoint, html) => {
    console.log(`[SignalR] Injecting into ${targetGuid} at ${mountPoint}`);

    // 1. Find Target
    // If targetGuid is "Shell" or empty, we search for the specific zone ID (mountPoint)
    let container = null;

    if (targetGuid && targetGuid !== "Shell") {
        // Find element by GUID
        const parent = document.querySelector(`[data-id="${targetGuid}"]`) || document.getElementById(targetGuid);
        if (parent) {
            // Find mount point inside parent
            container = parent.querySelector(`[data-mount-point="${mountPoint}"]`) || parent;
        }
    } else {
        // Root Level Injection (into a Zone)
        container = document.getElementById(mountPoint);
    }

    if (container) {
        // 2. Append HTML
        // We use insertAdjacentHTML to keep event listeners on existing elements intact
        container.insertAdjacentHTML('beforeend', html);
    } else {
        console.warn(`[SignalR] Target container not found: ${targetGuid} / ${mountPoint}`);
    }
});

connection.start().catch(err => console.error(err));

window.SoftwareCenter = {
    initController: function (moduleId, guid) {
        if (window.ModuleRegistry[moduleId] && window.ModuleRegistry[moduleId].init) {
            try {
                window.ModuleRegistry[moduleId].init(guid);
            } catch (err) {
                console.error(`[Host] Error in ${moduleId}:`, err);
            }
        }
    }
};