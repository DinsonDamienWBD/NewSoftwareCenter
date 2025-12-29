namespace Core.Messages
{
    /// <summary>
    /// Represents an intent to perform an action (Write/Change).
    /// </summary>
    public abstract class Command : MessageBase
    {
        /// <summary>Deadline for execution.</summary>
        public DateTime? ExecuteBy { get; set; }

        /// <summary>Unique Key to prevent duplicate processing (Idempotency).</summary>
        public string? IdempotencyKey { get; set; }

        /// <summary>Maximum time allowed for processing this command.</summary>
        public TimeSpan? Timeout { get; set; }
    }

    /// <summary>
    /// Standard batch processing
    /// </summary>
    /// <typeparam name="TItem"></typeparam>
    public class BatchCommand<TItem>(IEnumerable<TItem> items) : Command
    {
        /// <summary>
        /// List of items to process
        /// </summary>
        public List<TItem> Items { get; } = [.. items];

        /// <summary>
        /// Defines whether to stop on failure or continue
        /// </summary>
        public bool StopOnFirstFailure { get; set; } = false;
    }

    /// <summary>
    /// Represents a request for data (Read-Only).
    /// </summary>
    /// <typeparam name="TResponse">The expected response type.</typeparam>
    public abstract class Query<TResponse> : MessageBase { }

    /// <summary>
    /// Represents a fact that occurred in the past (Notification).
    /// </summary>
    public abstract class Event : MessageBase 
    {
        /// <summary>
        /// Gets or sets a value indicating whether this event should be broadcast 
        /// to other nodes in the cluster (e.g., via Redis or SignalR).
        /// </summary>
        public bool PropagateToCluster { get; set; } 
    }

    /// <summary>
    /// Represents a scheduled or deferred command.
    /// </summary>
    public abstract class Job : Command
    {
        /// <summary>Cron expression for recurring execution.</summary>
        public string? CronSchedule { get; set; }

        /// <summary>If true, the job state is persisted to disk across restarts.</summary>
        public bool IsPersistent { get; set; } = true;
    }

    /// <summary>
    /// Job progress reporting event.
    /// </summary>
    /// <remarks>
    /// Constructor for JobProgressEvent.
    /// </remarks>
    /// <param name="jobId"></param>
    /// <param name="percent"></param>
    /// <param name="message"></param>
    public class JobProgressEvent(Guid jobId, int percent, string message) : Event
    {
        /// <summary>
        /// ID of the job reporting progress.
        /// </summary>
        public Guid JobId { get; } = jobId;

        /// <summary>
        /// Percent complete (0-100).
        /// </summary>
        public int PercentComplete { get; } = percent;

        /// <summary>
        /// Status message describing the current progress.
        /// </summary>
        public string StatusMessage { get; } = message;
    }

    /// <summary>
    /// Optimized message type for large data transfer (The "Freight Train").
    /// </summary>
    public abstract class StreamMessage(Stream source, long length, string contentType) : MessageBase, IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Stream containing the raw data.
        /// </summary>
        public Stream DataStream { get; } = source;

        /// <summary>
        /// Length of the data in bytes.
        /// </summary>
        public long Length { get; } = length;

        /// <summary>
        /// Type of the content (e.g., "application/json", "image/png").
        /// </summary>
        public string ContentType { get; } = contentType;

        /// <summary>
        /// Content encoding used for compression (e.g., "gzip", "deflate").
        /// </summary>
        public string? ContentEncoding { get; protected set; }

        // Override Payload to expose the raw stream to the pipeline directly

        /// <summary>
        /// Payload containing the raw data stream.
        /// </summary>
        public override object Payload => DataStream;


        /// <summary>
        /// Disposes the data stream to free resources.
        /// </summary>
        public void Dispose()
        {
            DataStream?.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the data stream asynchronously to free resources.
        /// </summary>
        /// <returns></returns>
        public async ValueTask DisposeAsync()
        {
            if (DataStream != null)
            {
                await DataStream.DisposeAsync();
            }
            GC.SuppressFinalize(this);
        }
    }
}