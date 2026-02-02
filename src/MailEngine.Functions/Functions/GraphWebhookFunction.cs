using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MailEngine.Core.Interfaces;
using MailEngine.Core.Models;
using MailEngine.Infrastructure.ServiceBus;
using MailEngine.Functions.Webhooks;

namespace MailEngine.Functions.Functions;

public class GraphWebhookFunction
{
    private readonly ServiceBusPublisher _serviceBusPublisher;
    private readonly IWebhookValidator _webhookValidator;
    private readonly ILogger<GraphWebhookFunction> _logger;

    public GraphWebhookFunction(
        ServiceBusPublisher serviceBusPublisher,
        IWebhookValidator webhookValidator,
        ILogger<GraphWebhookFunction> logger)
    {
        _serviceBusPublisher = serviceBusPublisher;
        _webhookValidator = webhookValidator;
        _logger = logger;
    }

    [Function("GraphWebhookFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", "get", Route = "webhooks/graph")] HttpRequestData req,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = context.InvocationId;

            // Handle Graph webhook validation request
            if (req.Method == "GET")
            {
                var queryParam = req.Url.Query;
                if (queryParam.Contains("validationToken"))
                {
                    _logger.LogInformation("Received Graph webhook validation request. CorrelationId: {CorrelationId}", correlationId);
                    
                    var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", "text/plain");
                    await response.WriteStringAsync(queryParam.Replace("?validationToken=", ""), cancellationToken);
                    return response;
                }
            }

            // Handle Graph webhook notifications (POST)
            if (req.Method == "POST")
            {
                _logger.LogInformation("Received Graph webhook notification. CorrelationId: {CorrelationId}", correlationId);

                using var reader = new StreamReader(req.Body);
                var body = await reader.ReadToEndAsync(cancellationToken);

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                // Process each notification in the batch
                if (root.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var notification in valueElement.EnumerateArray())
                    {
                        if (!notification.TryGetProperty("resourceData", out var resourceData))
                            continue;

                        var resourceId = resourceData.TryGetProperty("id", out var id) ? id.GetString() : null;
                        
                        _logger.LogInformation("Processing Graph notification for resource: {ResourceId}", resourceId);

                        var userMailAccountId = Guid.NewGuid();

                        var readInboxEvent = new ReadInboxEvent
                        {
                            MessageId = Guid.NewGuid(),
                            CorrelationId = Guid.Parse(correlationId),
                            ProviderType = ProviderType.Outlook,
                            UserMailAccountId = userMailAccountId
                        };

                        await _serviceBusPublisher.PublishMessageAsync("mail-inbox-read", readInboxEvent, cancellationToken);
                    }
                }

                return req.CreateResponse(System.Net.HttpStatusCode.OK);
            }

            return req.CreateResponse(System.Net.HttpStatusCode.MethodNotAllowed);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Graph webhook processing was cancelled");
            return req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Graph webhook");
            return req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
        }
    }
}
