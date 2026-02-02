using System.Text.Json;
using Azure.Messaging.ServiceBus;
using MailEngine.Core.Models;

namespace MailEngine.Infrastructure.ServiceBus;

public class ServiceBusPublisher
{
    private readonly ServiceBusClient _client;

    public ServiceBusPublisher(string connectionString)
    {
        _client = new ServiceBusClient(connectionString);
    }

    public async Task PublishMessageAsync(string topicName, MailEvent mailEvent, CancellationToken cancellationToken = default)
    {
        var sender = _client.CreateSender(topicName);
        var message = new ServiceBusMessage(JsonSerializer.Serialize(mailEvent))
        {
            MessageId = mailEvent.MessageId.ToString(),
            CorrelationId = mailEvent.CorrelationId.ToString(),
            ApplicationProperties =
            {
                { "ProviderType", mailEvent.ProviderType.ToString() },
                { "UserMailAccountId", mailEvent.UserMailAccountId.ToString() }
            }
        };

        await sender.SendMessageAsync(message, cancellationToken);
    }
}
