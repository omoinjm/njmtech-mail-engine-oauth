using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using MailEngine.Core.Interfaces;
using MailEngine.Core.Models;
using System.Text;

namespace MailEngine.Providers.Gmail;

public class GmailMailProvider : IMailProvider
{
    private readonly ITokenRepository _tokenRepository;

    public GmailMailProvider(ITokenRepository tokenRepository)
    {
        _tokenRepository = tokenRepository;
    }

    public ProviderType ProviderType => ProviderType.Gmail;

    public async Task SendEmailAsync(SendMailEvent mailEvent, CancellationToken cancellationToken = default)
    {
        var token = await GetRefreshedToken(mailEvent.UserMailAccountId, cancellationToken);
        var service = CreateGmailService(token.AccessToken);

        var message = new Message
        {
            Raw = Base64UrlEncode(CreateMimeMessage(mailEvent))
        };

        await service.Users.Messages.Send(message, "me").ExecuteAsync(cancellationToken);
    }

    public Task ReadInboxAsync(ReadInboxEvent inboxEvent, CancellationToken cancellationToken = default)
    {
        // Implementation for reading inbox will be added later
        return Task.CompletedTask;
    }

    private async Task<OAuthToken> GetRefreshedToken(Guid userMailAccountId, CancellationToken cancellationToken)
    {
        var token = await _tokenRepository.GetTokenAsync(userMailAccountId, cancellationToken);
        // In a real app, you'd check for token expiration and refresh it using the refresh token.
        // For simplicity, we'll assume the token is always valid.
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
