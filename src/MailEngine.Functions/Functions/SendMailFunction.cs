using System.Text.Json;
using System.Net;
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
        catch (JsonException ex)
        {
            // PERMANENT: Corrupted message format - don't retry
            _logger.LogError(ex, "Unrecoverable: Invalid message format. Moving to Dead Letter Queue.");
            throw new InvalidOperationException("Message format is corrupted and cannot be processed", ex);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            // AUTH FAILURE: Invalid/expired credentials - don't retry, requires manual fix
            _logger.LogError(ex, "Unrecoverable: Authentication failed (401). User credentials need to be refreshed via OAuth app.");
            throw new InvalidOperationException("Authorization failed - credentials invalid or expired", ex);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest || ex.StatusCode == HttpStatusCode.BadGateway)
        {
            // PERMANENT: Bad request (invalid recipient, malformed email) - don't retry
            _logger.LogError(ex, "Unrecoverable: Bad request to email provider. Message data may be invalid.");
            throw new InvalidOperationException("Invalid request to email provider - message format error", ex);
        }
        catch (TimeoutException ex)
        {
            // TRANSIENT: Network timeout - Service Bus will retry automatically
            _logger.LogWarning(ex, "Transient error: Timeout connecting to email provider. Service Bus will retry.");
            throw;
        }
        catch (HttpRequestException ex) when (ex.InnerException is TimeoutException)
        {
            // TRANSIENT: Connection timeout - Service Bus will retry
            _logger.LogWarning(ex, "Transient error: Connection timeout. Service Bus will retry.");
            throw;
        }
        catch (Exception ex)
        {
            // UNKNOWN: Log full details for investigation
            _logger.LogError(ex, "Error processing SendMail event. Error type: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }
}
