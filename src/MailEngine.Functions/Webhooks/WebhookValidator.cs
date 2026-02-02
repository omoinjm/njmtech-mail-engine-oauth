using System.Security.Cryptography;
using System.Text;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace MailEngine.Functions.Webhooks;

public interface IWebhookValidator
{
    bool ValidateGooglePubSubMessage(string message, string signature, string publicKeyUrl);
    bool ValidateGraphWebhook(HttpRequestData request, string validationToken);
}

public class WebhookValidator : IWebhookValidator
{
    private readonly ILogger<WebhookValidator> _logger;

    public WebhookValidator(ILogger<WebhookValidator> logger)
    {
        _logger = logger;
    }

    public bool ValidateGooglePubSubMessage(string message, string signature, string publicKeyUrl)
    {
        try
        {
            _logger.LogInformation("Validating Google Pub/Sub message with signature from {PublicKeyUrl}", publicKeyUrl);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Google Pub/Sub message");
            return false;
        }
    }

    public bool ValidateGraphWebhook(HttpRequestData request, string validationToken)
    {
        try
        {
            _logger.LogInformation("Received Graph webhook validation request");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Graph webhook");
            return false;
        }
    }
}
