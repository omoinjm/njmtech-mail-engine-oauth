namespace MailEngine.Core.Models;

public class FailedMessage
{
    public Guid FailedMessageId { get; set; }
    public required string MessageIdTxt { get; set; }
    public required string TopicCd { get; set; }
    public required string SubscriptionTxt { get; set; }
    public required string ErrorMessageTxt { get; set; }
    public required string ErrorStackTraceTxt { get; set; }
    public DateTime FailedAtUtc { get; set; } = DateTime.UtcNow;
    public string StatusCd { get; set; } = "in-dlq"; // in-dlq, manual-retry-pending, resolved
    public int RetryCountNo { get; set; } = 0;
    public DateTime? ResolvedAtUtc { get; set; }
    public required string MessageContentTxt { get; set; } // Store the full message for retry
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
}
