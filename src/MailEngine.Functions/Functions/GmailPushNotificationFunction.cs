using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MailEngine.Core.Interfaces;
using MailEngine.Core.Models;
using MailEngine.Infrastructure.ServiceBus;
using MailEngine.Functions.Webhooks;

namespace MailEngine.Functions.Functions;

public class GmailPushNotificationFunction
{
    private readonly ServiceBusPublisher _serviceBusPublisher;
    private readonly IWebhookValidator _webhookValidator;
    private readonly ILogger<GmailPushNotificationFunction> _logger;

    public GmailPushNotificationFunction(
        ServiceBusPublisher serviceBusPublisher,
        IWebhookValidator webhookValidator,
        ILogger<GmailPushNotificationFunction> logger)
    {
        _serviceBusPublisher = serviceBusPublisher;
        _webhookValidator = webhookValidator;
        _logger = logger;
    }

    [Function("GmailPushNotificationFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "webhooks/gmail")] HttpRequestData req,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = context.InvocationId;
            _logger.LogInformation("Received Gmail push notification. CorrelationId: {CorrelationId}", correlationId);

            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync(cancellationToken);

            // Parse the incoming Google Pub/Sub message
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("message", out var messageElement))
            {
                _logger.LogWarning("Invalid Google Pub/Sub message format");
                return req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            }

            // Extract the data from the message (base64 encoded)
            if (!messageElement.TryGetProperty("data", out var dataElement))
            {
                _logger.LogWarning("No data in Google Pub/Sub message");
                return req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            }

            var base64Data = dataElement.GetString();
            if (string.IsNullOrEmpty(base64Data))
            {
                _logger.LogWarning("Empty data in Google Pub/Sub message");
                return req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            }

            var decodedData = System.Convert.FromBase64String(base64Data);
            var dataString = System.Text.Encoding.UTF8.GetString(decodedData);
            using var dataDoc = JsonDocument.Parse(dataString);
            var dataRoot = dataDoc.RootElement;

            // Extract user mail account ID from the notification
            if (!dataRoot.TryGetProperty("userId", out var userIdElement))
            {
                _logger.LogWarning("No userId in Gmail notification");
                return req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            }

            var userId = userIdElement.GetString();
            if (!Guid.TryParse(userId, out var userMailAccountId))
            {
                _logger.LogWarning("Invalid userId format: {UserId}", userId);
                return req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            }

            // Create a ReadInboxEvent and publish it to Service Bus
            var readInboxEvent = new ReadInboxEvent
            {
                MessageId = Guid.NewGuid(),
                CorrelationId = Guid.Parse(correlationId),
                ProviderType = ProviderType.Gmail,
                UserMailAccountId = userMailAccountId
            };

            await _serviceBusPublisher.PublishMessageAsync("mail-inbox-read", readInboxEvent, cancellationToken);
            _logger.LogInformation("Published ReadInboxEvent to Service Bus. CorrelationId: {CorrelationId}", correlationId);

            return req.CreateResponse(System.Net.HttpStatusCode.OK);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Gmail push notification processing was cancelled");
            return req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Gmail push notification");
            return req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
        }
    }
}
