using MailEngine.Core.Interfaces;

namespace MailEngine.Core.Models;

public abstract class MailEvent
{
    public Guid MessageId { get; set; } = Guid.NewGuid();
    public Guid CorrelationId { get; set; }
    public ProviderType ProviderType { get; set; }
    public Guid UserMailAccountId { get; set; }
    public string? IdempotencyKey { get; set; }
}
