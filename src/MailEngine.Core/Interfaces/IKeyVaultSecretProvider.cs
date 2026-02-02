namespace MailEngine.Core.Interfaces;

public interface IKeyVaultSecretProvider
{
    Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);
}
