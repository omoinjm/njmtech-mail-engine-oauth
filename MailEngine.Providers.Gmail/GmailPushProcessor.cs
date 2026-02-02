using System.Text.Json;
using MailEngine.Core.Interfaces;
using MailEngine.Core.Models;

namespace MailEngine.Providers.Gmail;

public class GmailPushProcessor
{
    private readonly IServiceBusPublisher _serviceBusPublisher;

    public GmailPushProcessor(IServiceBusPublisher serviceBusPublisher)
    {
        _serviceBusPublisher = serviceBusPublisher;
    }

    public async Task ProcessPushNotification(string requestBody, CancellationToken cancellationToken = default)
    {
        var pushMessage = JsonSerializer.Deserialize<GmailPushMessage>(requestBody);
        if (pushMessage != null)
        {
            var readInboxEvent = new ReadInboxEvent
            {
                ProviderType = ProviderType.Gmail,
                UserMailAccountId = ExtractUserMailAccountId(pushMessage), // You need to implement this
                CorrelationId = Guid.NewGuid() // Or extract from push if available
            };

            await _serviceBusPublisher.PublishMessageAsync("mail-inbox-read", readInboxEvent, cancellationToken);
        }
    }

    private Guid ExtractUserMailAccountId(GmailPushMessage pushMessage)
    {
        // In a real app, you would have a way to map the email address from the
        // push notification to your internal UserMailAccountId.
        // This could be a lookup in your database.
        // For this example, we'll return a new Guid.
        return Guid.NewGuid();
    }
}

public class GmailPushMessage
{
    public MessageData Message { get; set; }
    public string Subscription { get; set; }
}

public class MessageData
{
    public string Data { get; set; }
    public string MessageId { get; set; }
    public string PublishTime { get; set; }
}
