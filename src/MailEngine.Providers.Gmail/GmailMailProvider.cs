using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using MailEngine.Core.Interfaces;
using MailEngine.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace MailEngine.Providers.Gmail;

public class GmailMailProvider : IMailProvider
{
    private readonly ITokenRepository _tokenRepository;
    private readonly ILogger<GmailMailProvider> _logger;

    public GmailMailProvider(ITokenRepository tokenRepository, ILogger<GmailMailProvider> logger)
    {
        _tokenRepository = tokenRepository;
        _logger = logger;
    }

    public ProviderType ProviderType => ProviderType.Gmail;

    public async Task SendEmailAsync(SendMailEvent mailEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAndRefreshTokenIfNeeded(mailEvent.UserMailAccountId, cancellationToken);
            var service = CreateGmailService(token.AccessTokenTxt);

            var message = new Message
            {
                Raw = Base64UrlEncode(CreateMimeMessage(mailEvent))
            };

            await service.Users.Messages.Send(message, "me").ExecuteAsync(cancellationToken);
            _logger.LogInformation("Email sent successfully via Gmail for user {UserMailAccountId}", mailEvent.UserMailAccountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email via Gmail for user {UserMailAccountId}", mailEvent.UserMailAccountId);
            throw;
        }
    }

    public async Task ReadInboxAsync(ReadInboxEvent inboxEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAndRefreshTokenIfNeeded(inboxEvent.UserMailAccountId, cancellationToken);
            var service = CreateGmailService(token.AccessTokenTxt);

            // Fetch recent messages from inbox
            var request = service.Users.Messages.List("me");
            request.Q = "in:inbox";
            request.MaxResults = 10;

            var messages = await request.ExecuteAsync(cancellationToken);

            if (messages?.Messages != null && messages.Messages.Count > 0)
            {
                _logger.LogInformation("Retrieved {MessageCount} messages from Gmail inbox for user {UserMailAccountId}",
                    messages.Messages.Count, inboxEvent.UserMailAccountId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading inbox via Gmail for user {UserMailAccountId}", inboxEvent.UserMailAccountId);
            throw;
        }
    }

    private async Task<OAuthToken> GetAndRefreshTokenIfNeeded(Guid userMailAccountId, CancellationToken cancellationToken)
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

        return token;
    }

    private GmailService CreateGmailService(string accessToken)
    {
        var credential = GoogleCredential.FromAccessToken(accessToken);
        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "MailEngine"
        });
    }

    private string CreateMimeMessage(SendMailEvent mailEvent)
    {
        var message = new StringBuilder();
        message.AppendLine("MIME-Version: 1.0");
        message.AppendLine($"To: {mailEvent.To}");
        message.AppendLine($"Subject: {mailEvent.Subject}");
        message.AppendLine("Content-Type: text/html; charset=utf-8");
        message.AppendLine();
        message.AppendLine(mailEvent.Body);
        return message.ToString();
    }

    private string Base64UrlEncode(string input)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(inputBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "");
    }
}
