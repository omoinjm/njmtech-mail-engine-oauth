using MailEngine.Core.Interfaces;
using MailEngine.Core.Models;

namespace MailEngine.Functions.Dispatching;

public class MailEventDispatcher : IMailEventDispatcher
{
    private readonly IMailProviderFactory _mailProviderFactory;
    private readonly ProviderConcurrencyLimiter _concurrencyLimiter;

    public MailEventDispatcher(IMailProviderFactory mailProviderFactory, ProviderConcurrencyLimiter concurrencyLimiter)
    {
        _mailProviderFactory = mailProviderFactory;
        _concurrencyLimiter = concurrencyLimiter;
    }

    public async Task HandleEventAsync(MailEvent mailEvent, CancellationToken cancellationToken = default)
    {
        await _concurrencyLimiter.WaitAsync(mailEvent.ProviderType, cancellationToken);
        try
        {
            var provider = _mailProviderFactory.GetProvider(mailEvent.ProviderType);
            switch (mailEvent)
            {
                case SendMailEvent sendMailEvent:
                    await provider.SendEmailAsync(sendMailEvent, cancellationToken);
                    break;
                case ReadInboxEvent readInboxEvent:
                    await provider.ReadInboxAsync(readInboxEvent, cancellationToken);
                    break;
                default:
                    throw new NotSupportedException($"Mail event of type {mailEvent.GetType().Name} is not supported.");
            }
        }
        finally
        {
            _concurrencyLimiter.Release(mailEvent.ProviderType);
        }
    }
}
