using MailEngine.Core.Interfaces;
using MailEngine.Core.Models;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Azure.Identity;

namespace MailEngine.Providers.Outlook;

public class OutlookMailProvider : IMailProvider
{
    private readonly ITokenRepository _tokenRepository;

    public OutlookMailProvider(ITokenRepository tokenRepository)
    {
        _tokenRepository = tokenRepository;
    }

    public ProviderType ProviderType => ProviderType.Outlook;

    public async Task SendEmailAsync(SendMailEvent mailEvent, CancellationToken cancellationToken = default)
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

        await graphServiceClient.Me.SendMail(new Microsoft.Graph.Me.SendMail.SendMailPostRequestBody { Message = message }).Request().PostAsync(cancellationToken);
    }

    public Task ReadInboxAsync(ReadInboxEvent inboxEvent, CancellationToken cancellationToken = default)
    {
        // Implementation for reading inbox will be added later
        return Task.CompletedTask;
    }

    private async Task<GraphServiceClient> CreateGraphServiceClient(Guid userMailAccountId, CancellationToken cancellationToken)
    {
        var token = await _tokenRepository.GetTokenAsync(userMailAccountId, cancellationToken);
        
        var credentials = new OnBehalfOfCredential(
            tenantId: "YOUR_TENANT_ID", // Replace with your tenant ID
            clientId: "YOUR_CLIENT_ID", // Replace with your client ID
            clientSecret: "YOUR_CLIENT_SECRET", // Replace with your client secret
            userAssertion: new UserAssertion(token.AccessToken));

        return new GraphServiceClient(credentials);
    }
}
