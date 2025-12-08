using Microsoft.AspNetCore.SignalR;
using SoftwareCenter.Core.UI;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SoftwareCenter.Host
{
    /// <summary>
    /// SignalR Hub for broadcasting UI state updates to connected clients.
    /// </summary>
    public class UIHub : Hub
    {
        /// <summary>
        /// Sends a snapshot of the current UI state to all connected clients.
        /// </summary>
        /// <param name="uiState">A collection of all current UI elements.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public async Task SendUIUpdate(ICollection<UIElement> uiState)
        {
            await Clients.All.SendAsync("ReceiveUIUpdate", uiState);
        }
    }
}
