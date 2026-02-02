namespace MailEngine.Core.Models;

public class FailedMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; }
    public string Topic { get; set; }
    public string Subscription { get; set; }
    public string ErrorMessage { get; set; }
    public string ErrorStackTrace { get; set; }
    public DateTime FailedAtUtc { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "in-dlq"; // in-dlq, manual-retry-pending, resolved
    public int RetryCount { get; set; } = 0;
    public DateTime? ResolvedAtUtc { get; set; }
    public string MessageContent { get; set; } // Store the full message for retry
}
