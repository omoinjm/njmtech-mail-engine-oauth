using MailEngine.Core.Models;

namespace MailEngine.Core.Interfaces;

public enum ProviderType
{
    Gmail,
    Outlook
}

public interface IMailProvider
{
    ProviderType ProviderType { get; }
    Task SendEmailAsync(SendMailEvent mailEvent, CancellationToken cancellationToken = default);
    Task ReadInboxAsync(ReadInboxEvent inboxEvent, CancellationToken cancellationToken = default);
}
