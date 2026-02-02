using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using MailEngine.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace MailEngine.Infrastructure.KeyVault;

public class KeyVaultSecretProvider : IKeyVaultSecretProvider
{
    private readonly SecretClient _client;
    private readonly ILogger<KeyVaultSecretProvider> _logger;

    public KeyVaultSecretProvider(string keyVaultUri, ILogger<KeyVaultSecretProvider> logger)
    {
        _logger = logger;
        var credential = new DefaultAzureCredential();
        _client = new SecretClient(new Uri(keyVaultUri), credential);
    }

    public async Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        try
        {
            var secret = await _client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            return secret.Value.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret '{SecretName}' from Key Vault", secretName);
            throw;
        }
    }
}
