using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MailEngine.Core.Interfaces;
using MailEngine.Core.Models;

namespace MailEngine.Functions.Functions;

public class SendMailFunction
{
    private readonly IMailEventHandler _eventHandler;
    private readonly ILogger<SendMailFunction> _logger;

    public SendMailFunction(IMailEventHandler eventHandler, ILogger<SendMailFunction> logger)
    {
        _eventHandler = eventHandler;
        _logger = logger;
    }

    [Function("SendMailFunction")]
    public async Task Run(
        [ServiceBusTrigger("mail-send", "gmail", Connection = "AzureServiceBus:ConnectionString")]
        string message,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = context.InvocationId;
            _logger.LogInformation("Processing SendMail event. CorrelationId: {CorrelationId}", correlationId);

            var mailEvent = JsonSerializer.Deserialize<SendMailEvent>(message);
            if (mailEvent == null)
            {
                _logger.LogError("Failed to deserialize SendMailEvent");
                throw new InvalidOperationException("Invalid SendMailEvent format");
            }

            await _eventHandler.HandleEventAsync(mailEvent, cancellationToken);
            _logger.LogInformation("SendMail event processed successfully. CorrelationId: {CorrelationId}", correlationId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("SendMail function was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SendMail event");
            throw;
        }
    }
}
