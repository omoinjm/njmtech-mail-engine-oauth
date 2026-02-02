namespace MailEngine.Core.Models;

public class FailedMessage
{
    public Guid FailedMessageId { get; set; } = Guid.NewGuid();
    public string MessageIdTxt { get; set; }
    public string TopicCd { get; set; }
    public string SubscriptionTxt { get; set; }
    public string ErrorMessageTxt { get; set; }
    public string ErrorStackTraceTxt { get; set; }
    public DateTime FailedAtUtc { get; set; } = DateTime.UtcNow;
    public string StatusCd { get; set; } = "in-dlq"; // in-dlq, manual-retry-pending, resolved
    public int RetryCountNo { get; set; } = 0;
    public DateTime? ResolvedAtUtc { get; set; }
    public string MessageContentTxt { get; set; } // Store the full message for retry
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
}
