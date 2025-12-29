using Microsoft.AspNetCore.SignalR;
using FrontendManager.Services;

namespace FrontendManager.Hubs
{
    /// <summary>
    /// Hub interface for frontend client communication.
    /// </summary>
    public interface IFrontendClient
    {
        /// <summary>
        /// Receive a message from the server.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="payload"></param>
        /// <returns></returns>
        Task ReceiveMessage(string type, object payload);

        /// <summary>
        /// Inject HTML fragment into the frontend at the specified mount point.
        /// </summary>
        /// <param name="targetGuid"></param>
        /// <param name="mountPoint"></param>
        /// <param name="html"></param>
        /// <returns></returns>
        Task InjectFragment(string targetGuid, string mountPoint, string html);

        /// <summary>
        /// Update the state of a target component in the frontend.
        /// </summary>
        /// <param name="targetGuid"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        Task UpdateState(Guid targetGuid, object state);
    }

    /// <summary>
    /// Hub for managing frontend client connections.
    /// </summary>
    public class FrontendHub(IConnectionManager connectionManager) : Hub<IFrontendClient>
    {
        private readonly IConnectionManager _connectionManager = connectionManager;

        /// <summary>
        /// Called when a client connects to the hub.
        /// </summary>
        /// <returns></returns>
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();

            // 1. Identification
            // In a real scenario, this comes from Context.UserIdentifier (JWT/Cookie claims).
            // For V1 (Anonymous/Single User), we default to "Guest" or a query param.
            var userId = Context.UserIdentifier ?? Context.GetHttpContext()?.Request.Query["userId"].ToString() ?? "Guest";

            // 2. Map Connection
            _connectionManager.AddConnection(userId, Context.ConnectionId);

            // 3. (Optional) Notify user they are connected
            await Clients.Caller.ReceiveMessage("System", new { Status = "Connected", UserId = userId });
        }

        /// <summary>
        /// Called when a client disconnects from the hub.
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _connectionManager.RemoveConnection(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}