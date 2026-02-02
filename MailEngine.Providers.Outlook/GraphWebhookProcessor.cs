using System.Text.Json;
using MailEngine.Core.Interfaces;
using MailEngine.Core.Models;

namespace MailEngine.Providers.Outlook;

public class GraphWebhookProcessor
{
    private readonly IServiceBusPublisher _serviceBusPublisher;

    public GraphWebhookProcessor(IServiceBusPublisher serviceBusPublisher)
    {
        _serviceBusPublisher = serviceBusPublisher;
    }

    public async Task ProcessWebhookNotification(string requestBody, CancellationToken cancellationToken = default)
    {
        var notification = JsonSerializer.Deserialize<GraphNotification>(requestBody);
        if (notification != null && notification.Value != null)
        {
            foreach (var value in notification.Value)
            {
                var readInboxEvent = new ReadInboxEvent
                {
                    ProviderType = ProviderType.Outlook,
                    UserMailAccountId = Guid.Parse(value.ResourceData.OdataId), // This needs to be mapped correctly
                    CorrelationId = Guid.NewGuid() // Or extract from notification if available
                };

                await _serviceBusPublisher.PublishMessageAsync("mail-inbox-read", readInboxEvent, cancellationToken);
            }
        }
    }
}

public class GraphNotification
{
    public List<GraphNotificationValue> Value { get; set; }
}

public class GraphNotificationValue
{
    public string SubscriptionId { get; set; }
    public long SubscriptionExpirationDateTime { get; set; }
    public string ChangeType { get; set; }
    public string Resource { get; set; }
    public ResourceData ResourceData { get; set; }
}

public class ResourceData
{
    public string OdataType { get; set; }
    public string OdataId { get; set; }
    public string Etag { get; set; }
    public string Id { get; set; }
}
