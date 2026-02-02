namespace MailEngine.Core.Interfaces;

public interface IMailProviderFactory
{
    IMailProvider GetProvider(ProviderType providerType);
}
