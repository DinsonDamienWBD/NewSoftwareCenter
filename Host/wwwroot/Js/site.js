document.addEventListener("DOMContentLoaded", () => {
    console.log("Software Center SPA shell loaded.");

    const navContainer = document.getElementById("main-nav");
    const contentContainer = document.getElementById("content-area");

    // --- SignalR Connection ---
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/ui-hub")
        .withAutomaticReconnect()
        .build();

    // --- Client-Side Event Handlers ---

    connection.on("RenderNavButton", (button) => {
        console.log("Rendering Nav Button:", button);
        const btn = document.createElement("button");
        btn.id = button.id;
        btn.className = "nav-button";
        btn.innerHTML = `<i class="${button.icon || ''}"></i> ${button.label}`;
        btn.dataset.navTarget = button.id;
        btn.style.order = button.priority;

        btn.addEventListener("click", () => {
            // Deactivate all other buttons and containers
            document.querySelectorAll('.nav-button.active').forEach(b => b.classList.remove('active'));
            document.querySelectorAll('.content-container.active').forEach(c => c.classList.remove('active'));

            // Activate the clicked button and its corresponding container
            btn.classList.add('active');
            const targetContainer = document.querySelector(`.content-container[data-associated-nav='${button.id}']`);
            if (targetContainer) {
                targetContainer.classList.add('active');
            } else {
                // If no container is found, it's safer to show nothing than a stale view.
                // This handles cases where a nav button was created but its container failed.
                console.warn(`No content container found for nav button ${button.id}`);
            }
        });

        navContainer.appendChild(btn);
    });

    connection.on("RenderContentContainer", (container) => {
        console.log("Rendering Content Container:", container);
        const div = document.createElement("div");
        div.id = container.id;
        div.className = "content-container";
        div.dataset.associatedNav = container.associatedNavButtonId;

        let headerHtml = '';
        if (container.hasSpa) {
            headerHtml = `
                <div class="container-header">
                    <button class="pop-out-button" title="Open in new tab" onclick="window.open('${container.spaUrl}', '_blank')">
                        ↗
                    </button>
                </div>`;
        }

        div.innerHTML = `${headerHtml}<div class="container-body" id="body-${container.id}"></div>`;
        contentContainer.appendChild(div);
    });

    connection.on("UpdateElementContent", (targetElementId, htmlContent) => {
        console.log(`Updating content for ${targetElementId}`);
        // The content is placed inside the container's body, not replacing the container itself
        const bodyId = `body-${targetElementId}`;
        let target = document.getElementById(bodyId);

        // If it's not a container body, it might be a card or other element directly
        if (!target) {
            target = document.getElementById(targetElementId);
        }

        if (target) {
            target.innerHTML = htmlContent;
        } else {
            console.error(`Target element '${targetElementId}' not found for content update.`);
        }
    });

    connection.on("RemoveElement", (elementId) => {
        console.log(`Removing element ${elementId}`);
        const element = document.getElementById(elementId);
        if (element) {
            element.remove();
        }
    });

    // --- Global Interaction Handler ---
    document.addEventListener('click', (e) => {
        const targetButton = e.target.closest('button[data-owner-id][data-action]');
        if (!targetButton) {
            return;
        }

        const payload = {
            ownerId: targetButton.dataset.ownerId,
            action: targetButton.dataset.action
        };

        // Gather any additional data attributes from the button
        for (const key in targetButton.dataset) {
            if (key !== 'ownerId' && key !== 'action') {
                payload[key] = targetButton.dataset[key];
            }
        }

        console.log("Sending interaction:", payload);

        fetch('/api/ui/interact', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        }).then(res => {
            if (!res.ok) {
                console.error(`Interaction failed for action '${payload.action}'. Server responded with status: ${res.status}`);
            } else {
                console.log(`Interaction successful for action '${payload.action}'.`);
            }
        }).catch(err => console.error('Interaction fetch failed:', err));
    });

    // --- Start Connection ---
    async function start() {
        try {
            await connection.start();
            console.log("SignalR Connected.");
            // Clear placeholder text on successful connection
            contentContainer.innerHTML = '';
        } catch (err) {
            console.error(err);
            setTimeout(start, 5000);
        }
    };

    connection.onclose(start);
    start();
});