const connection = new signalR.HubConnectionBuilder()
    .withUrl("/uihub")
    .build();

connection.on("ReceiveNotification", (message) => {
    console.log("Notification received: ", message);
    // Here you would typically update the UI based on the message
    const appDiv = document.getElementById("app");
    if (appDiv) {
        appDiv.innerHTML += `<p>${message}</p>`;
    }
});

async function start() {
    try {
        await connection.start();
        console.log("SignalR Connected.");
    } catch (err) {
        console.error(err);
        setTimeout(start, 5000); // Restart connection after 5 seconds
    }
};

connection.onclose(async () => {
    await start();
});

// Start the connection.
start();

// Example: send a command to the backend
// This is a placeholder and assumes a command structure
async function sendCommand(commandName, payload) {
    try {
        const response = await fetch(`/api/dispatch/${commandName}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(payload)
        });
        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`HTTP error! status: ${response.status}, message: ${errorText}`);
        }
        const result = await response.json();
        console.log(`Command '${commandName}' dispatched successfully. Result:`, result);
        return result;
    } catch (error) {
        console.error(`Error dispatching command '${commandName}':`, error);
        throw error;
    }
}
