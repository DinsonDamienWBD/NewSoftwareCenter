using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataWarehouse.SDK.AI.Safety
{
    /// <summary>
    /// Manages approval requests for sensitive AI operations.
    /// Implements human-in-the-loop safety for high-risk actions.
    ///
    /// Used by AI Runtime to:
    /// - Request approval before executing sensitive capabilities
    /// - Queue actions for human review
    /// - Auto-approve safe operations
    /// - Audit all approval decisions
    /// - Enforce safety policies
    ///
    /// Approval triggers:
    /// - Cost exceeds threshold
    /// - Sensitive capability (delete, admin, security)
    /// - User-defined approval rules
    /// - First-time capability usage
    /// </summary>
    public class ApprovalQueue
    {
        private readonly Dictionary<string, ApprovalRequest> _pendingRequests = new();
        private readonly List<ApprovalRecord> _history = new();
        private readonly AutoApprovalPolicy _autoApprovalPolicy;
        private readonly object _lock = new();
        private int _nextRequestId = 1;

        public ApprovalQueue(AutoApprovalPolicy? autoApprovalPolicy = null)
        {
            _autoApprovalPolicy = autoApprovalPolicy ?? new AutoApprovalPolicy();
        }

        /// <summary>
        /// Submits a request for approval.
        /// Returns immediately if auto-approved, otherwise queues for human review.
        ///
        /// Use cases:
        /// - AI wants to delete data → requires approval
        /// - AI wants to compress file (safe) → auto-approved
        /// - AI wants to spend $10 (over budget) → requires approval
        /// </summary>
        /// <param name="request">Approval request details.</param>
        /// <returns>Approval result (may be immediate or pending).</returns>
        public async Task<ApprovalResult> RequestApprovalAsync(ApprovalRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            // Assign ID
            request.Id = GenerateRequestId();
            request.SubmittedAt = DateTime.UtcNow;
            request.Status = ApprovalStatus.Pending;

            // Check auto-approval policy
            if (_autoApprovalPolicy.ShouldAutoApprove(request))
            {
                var autoResult = new ApprovalResult
                {
                    RequestId = request.Id,
                    Approved = true,
                    AutoApproved = true,
                    Reason = "Auto-approved by policy",
                    DecidedAt = DateTime.UtcNow
                };

                request.Status = ApprovalStatus.Approved;
                RecordDecision(request, autoResult);

                return autoResult;
            }

            // Queue for human approval
            lock (_lock)
            {
                _pendingRequests[request.Id] = request;
            }

            // In real implementation, this would notify user/admin
            // For now, return pending status
            return new ApprovalResult
            {
                RequestId = request.Id,
                Approved = false,
                AutoApproved = false,
                Reason = "Pending human approval",
                DecidedAt = null
            };
        }

        /// <summary>
        /// Approves a pending request.
        /// Called by user/admin through UI.
        /// </summary>
        /// <param name="requestId">Request ID to approve.</param>
        /// <param name="approverId">ID of person approving.</param>
        /// <param name="reason">Reason for approval (optional).</param>
        public ApprovalResult Approve(string requestId, string approverId, string? reason = null)
        {
            ApprovalRequest? request;
            lock (_lock)
            {
                if (!_pendingRequests.TryGetValue(requestId, out request))
                {
                    throw new ArgumentException($"Request '{requestId}' not found");
                }

                request.Status = ApprovalStatus.Approved;
                _pendingRequests.Remove(requestId);
            }

            var result = new ApprovalResult
            {
                RequestId = requestId,
                Approved = true,
                AutoApproved = false,
                ApproverId = approverId,
                Reason = reason ?? "Approved by user",
                DecidedAt = DateTime.UtcNow
            };

            RecordDecision(request, result);
            return result;
        }

        /// <summary>
        /// Rejects a pending request.
        /// Called by user/admin through UI.
        /// </summary>
        /// <param name="requestId">Request ID to reject.</param>
        /// <param name="approverId">ID of person rejecting.</param>
        /// <param name="reason">Reason for rejection.</param>
        public ApprovalResult Reject(string requestId, string approverId, string reason)
        {
            ApprovalRequest? request;
            lock (_lock)
            {
                if (!_pendingRequests.TryGetValue(requestId, out request))
                {
                    throw new ArgumentException($"Request '{requestId}' not found");
                }

                request.Status = ApprovalStatus.Rejected;
                _pendingRequests.Remove(requestId);
            }

            var result = new ApprovalResult
            {
                RequestId = requestId,
                Approved = false,
                AutoApproved = false,
                ApproverId = approverId,
                Reason = reason,
                DecidedAt = DateTime.UtcNow
            };

            RecordDecision(request, result);
            return result;
        }

        /// <summary>
        /// Gets all pending approval requests.
        /// Used by UI to display approval queue.
        /// </summary>
        /// <returns>List of pending requests.</returns>
        public List<ApprovalRequest> GetPendingRequests()
        {
            lock (_lock)
            {
                return _pendingRequests.Values.ToList();
            }
        }

        /// <summary>
        /// Gets approval history (approved and rejected requests).
        /// </summary>
        /// <param name="limit">Maximum number of records to return.</param>
        /// <returns>List of approval records.</returns>
        public List<ApprovalRecord> GetHistory(int limit = 100)
        {
            lock (_lock)
            {
                return _history
                    .OrderByDescending(r => r.Request.SubmittedAt)
                    .Take(limit)
                    .ToList();
            }
        }

        /// <summary>
        /// Waits for a decision on a pending request.
        /// Blocks until approved or rejected.
        /// </summary>
        /// <param name="requestId">Request ID to wait for.</param>
        /// <param name="timeoutMs">Timeout in milliseconds.</param>
        /// <returns>Approval result, or null if timeout.</returns>
        public async Task<ApprovalResult?> WaitForDecisionAsync(string requestId, int timeoutMs = 60000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (DateTime.UtcNow < deadline)
            {
                // Check if request has been decided
                lock (_lock)
                {
                    var record = _history.FirstOrDefault(r => r.Request.Id == requestId);
                    if (record != null)
                    {
                        return record.Result;
                    }
                }

                // Wait a bit before checking again
                await Task.Delay(500);
            }

            // Timeout
            return null;
        }

        /// <summary>
        /// Clears all pending requests.
        /// Used for testing.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _pendingRequests.Clear();
            }
        }

        /// <summary>
        /// Records an approval decision in history.
        /// </summary>
        private void RecordDecision(ApprovalRequest request, ApprovalResult result)
        {
            lock (_lock)
            {
                _history.Add(new ApprovalRecord
                {
                    Request = request,
                    Result = result
                });

                // Keep only recent history (last 1000 records)
                if (_history.Count > 1000)
                {
                    _history.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Generates unique request ID.
        /// </summary>
        private string GenerateRequestId()
        {
            lock (_lock)
            {
                return $"approval-{_nextRequestId++}";
            }
        }
    }

    /// <summary>
    /// Request for approval to execute an action.
    /// </summary>
    public class ApprovalRequest
    {
        /// <summary>Unique request ID.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Type of action requiring approval.</summary>
        public string ActionType { get; set; } = string.Empty;

        /// <summary>Description of the action.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Capability ID to execute.</summary>
        public string? CapabilityId { get; set; }

        /// <summary>User requesting the action.</summary>
        public string? UserId { get; set; }

        /// <summary>Estimated cost in USD.</summary>
        public decimal? EstimatedCostUsd { get; set; }

        /// <summary>Risk level (low, medium, high).</summary>
        public string RiskLevel { get; set; } = "medium";

        /// <summary>Justification for the action.</summary>
        public string? Justification { get; set; }

        /// <summary>Additional metadata.</summary>
        public Dictionary<string, object> Metadata { get; init; } = new();

        /// <summary>When request was submitted.</summary>
        public DateTime SubmittedAt { get; set; }

        /// <summary>Current status.</summary>
        public ApprovalStatus Status { get; set; }
    }

    /// <summary>
    /// Result of an approval decision.
    /// </summary>
    public class ApprovalResult
    {
        /// <summary>Request ID this result is for.</summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>Whether request was approved.</summary>
        public bool Approved { get; set; }

        /// <summary>Whether this was auto-approved (no human).</summary>
        public bool AutoApproved { get; set; }

        /// <summary>ID of approver (null if auto-approved).</summary>
        public string? ApproverId { get; set; }

        /// <summary>Reason for decision.</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>When decision was made.</summary>
        public DateTime? DecidedAt { get; set; }
    }

    /// <summary>
    /// Approval status.
    /// </summary>
    public enum ApprovalStatus
    {
        Pending,
        Approved,
        Rejected
    }

    /// <summary>
    /// Record of an approval decision.
    /// </summary>
    public class ApprovalRecord
    {
        public ApprovalRequest Request { get; set; } = new();
        public ApprovalResult Result { get; set; } = new();
    }
}
