using Microsoft.Extensions.DependencyInjection;
using MailEngine.Core.Interfaces;
using MailEngine.Providers.Gmail;
using MailEngine.Providers.Outlook;

namespace MailEngine.Infrastructure.Factories;

public class MailProviderFactory : IMailProviderFactory
{
    private readonly IServiceProvider _serviceProvider;

    public MailProviderFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IMailProvider GetProvider(ProviderType providerType)
    {
        return providerType switch
        {
            ProviderType.Gmail => _serviceProvider.GetRequiredService<GmailMailProvider>(),
            ProviderType.Outlook => _serviceProvider.GetRequiredService<OutlookMailProvider>(),
            _ => throw new ArgumentException($"Unknown provider type: {providerType}", nameof(providerType))
        };
    }
}
