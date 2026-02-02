using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MailEngine.Core.Interfaces;
using MailEngine.Core.Models;

namespace MailEngine.Functions.Functions;

public class ReadInboxFunction
{
    private readonly IMailEventHandler _eventHandler;
    private readonly ILogger<ReadInboxFunction> _logger;

    public ReadInboxFunction(IMailEventHandler eventHandler, ILogger<ReadInboxFunction> logger)
    {
        _eventHandler = eventHandler;
        _logger = logger;
    }

    [Function("ReadInboxFunction")]
    public async Task Run(
        [ServiceBusTrigger("mail-inbox-read", "gmail", Connection = "AzureServiceBus:ConnectionString")]
        string message,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = context.InvocationId;
            _logger.LogInformation("Processing ReadInbox event. CorrelationId: {CorrelationId}", correlationId);

            var mailEvent = JsonSerializer.Deserialize<ReadInboxEvent>(message);
            if (mailEvent == null)
            {
                _logger.LogError("Failed to deserialize ReadInboxEvent");
                throw new InvalidOperationException("Invalid ReadInboxEvent format");
            }

            await _eventHandler.HandleEventAsync(mailEvent, cancellationToken);
            _logger.LogInformation("ReadInbox event processed successfully. CorrelationId: {CorrelationId}", correlationId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ReadInbox function was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ReadInbox event");
            throw;
        }
    }
}
