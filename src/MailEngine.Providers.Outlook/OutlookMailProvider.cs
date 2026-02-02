using MailEngine.Core.Interfaces;
using MailEngine.Core.Models;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace MailEngine.Providers.Outlook;

public class OutlookMailProvider : IMailProvider
{
    private readonly ITokenRepository _tokenRepository;
    private readonly IKeyVaultSecretProvider? _keyVaultProvider;
    private readonly ILogger<OutlookMailProvider> _logger;

    public OutlookMailProvider(
        ITokenRepository tokenRepository,
        IKeyVaultSecretProvider? keyVaultProvider,
        ILogger<OutlookMailProvider> logger)
    {
        _tokenRepository = tokenRepository;
        _keyVaultProvider = keyVaultProvider;
        _logger = logger;
    }

    public ProviderType ProviderType => ProviderType.Outlook;

    public async Task SendEmailAsync(SendMailEvent mailEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var graphServiceClient = await CreateGraphServiceClient(mailEvent.UserMailAccountId, cancellationToken);

            var message = new Message
            {
                Subject = mailEvent.Subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = mailEvent.Body
                },
                ToRecipients = new List<Recipient>
                {
                    new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = mailEvent.To
                        }
                    }
                }
            };

            var requestBody = new Microsoft.Graph.Me.SendMail.SendMailPostRequestBody { Message = message };
            await graphServiceClient.Me.SendMail.PostAsync(requestBody, cancellationToken: cancellationToken);
            
            _logger.LogInformation("Email sent successfully via Outlook for user {UserMailAccountId}", mailEvent.UserMailAccountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email via Outlook for user {UserMailAccountId}", mailEvent.UserMailAccountId);
            throw;
        }
    }

    public async Task ReadInboxAsync(ReadInboxEvent inboxEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var graphServiceClient = await CreateGraphServiceClient(inboxEvent.UserMailAccountId, cancellationToken);

            // Fetch recent messages from inbox
            var messages = await graphServiceClient.Me.MailFolders["inbox"].Messages
                .GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Top = 10;
                }, cancellationToken);

            if (messages?.Value != null)
            {
                _logger.LogInformation("Retrieved {MessageCount} messages from Outlook inbox for user {UserMailAccountId}",
                    messages.Value.Count, inboxEvent.UserMailAccountId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading inbox via Outlook for user {UserMailAccountId}", inboxEvent.UserMailAccountId);
            throw;
        }
    }

    private async Task<GraphServiceClient> CreateGraphServiceClient(Guid userMailAccountId, CancellationToken cancellationToken)
    {
        var token = await _tokenRepository.GetTokenAsync(userMailAccountId, cancellationToken);
        
        if (token == null)
        {
            throw new InvalidOperationException($"No token found for user {userMailAccountId}");
        }

        if (token.ExpiresAtUtc < DateTime.UtcNow.AddMinutes(5))
        {
            _logger.LogInformation("Token expired or expiring soon for user {UserMailAccountId}, attempting refresh...", userMailAccountId);
        }

        var credential = new ClientSecretCredential(
            tenantId: await GetSecretAsync("outlook-tenant-id", cancellationToken),
            clientId: await GetSecretAsync("outlook-client-id", cancellationToken),
            clientSecret: await GetSecretAsync("outlook-client-secret", cancellationToken));

        return new GraphServiceClient(credential);
    }

    private async Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken)
    {
        // Try Key Vault first if configured
        if (_keyVaultProvider != null)
        {
            try
            {
                return await _keyVaultProvider.GetSecretAsync(secretName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve secret '{SecretName}' from Key Vault, falling back to environment variables", secretName);
            }
        }

        // Fall back to environment variables
        var envVarName = ConvertSecretNameToEnvVar(secretName);
        var envValue = Environment.GetEnvironmentVariable(envVarName);
        
        if (!string.IsNullOrEmpty(envValue))
        {
            return envValue;
        }

        throw new InvalidOperationException(
            $"Secret '{secretName}' not found. Either configure Key Vault or set environment variable '{envVarName}'");
    }

    private static string ConvertSecretNameToEnvVar(string secretName)
    {
        // Convert "outlook-tenant-id" to "OUTLOOK_TENANT_ID"
        return secretName.ToUpper().Replace("-", "_");
    }
}
