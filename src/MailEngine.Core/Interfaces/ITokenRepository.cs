using MailEngine.Core.Models;

namespace MailEngine.Core.Interfaces;

public interface ITokenRepository
{
    Task<OAuthToken?> GetTokenAsync(Guid userMailAccountId, CancellationToken cancellationToken = default);
    Task<OAuthToken> SaveTokenAsync(OAuthToken token, CancellationToken cancellationToken = default);
}
