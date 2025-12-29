using Core.Frontend.Contracts;
using Core.Frontend.Messages;
using Core.Pipeline;
using Core.Services;
using Host.Modules;

namespace Host.Services
{
    // A simple event to carry the notification payload
    /// <summary>
    /// Notification event sent to the UI layer.
    /// </summary>
    public class NotificationEvent : MessageBase, IUiEvent
    {
        /// <summary>
        /// The title of the notification.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Message content of the notification.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Severity level of the notification.
        /// </summary>
        public string Severity { get; set; } = "Info";
    }

    /// <summary>
    /// Notification service that sends notifications to the UI layer.
    /// </summary>
    /// <param name="frontend"></param>
    public class UiNotificationService(IFrontendPipeline frontend) : INotificationService
    {
        private readonly IFrontendPipeline _frontend = frontend;

        /// <summary>
        /// Notify the UI with a message.
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <param name="severity"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task NotifyAsync(string title, string message, NotificationSeverity severity, CancellationToken ct)
        {
            await _frontend.PublishAsync(new NotificationEvent
            {
                Title = title,
                Message = message,
                Severity = severity.ToString()
            }, new RequestContext("System"), ct);
        }
    }
}