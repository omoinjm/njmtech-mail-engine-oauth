using MailEngine.Core.Interfaces;
using MailEngine.Core.Models;
using MailEngine.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MailEngine.Infrastructure.TokenStore;

public class TokenRepository : ITokenRepository
{
    private readonly MailEngineDbContext _context;

    public TokenRepository(MailEngineDbContext context)
    {
        _context = context;
    }

    public async Task<OAuthToken> GetTokenAsync(Guid userMailAccountId, CancellationToken cancellationToken = default)
    {
        return await _context.OAuthTokens
            .FirstOrDefaultAsync(t => t.UserMailAccountId == userMailAccountId, cancellationToken);
    }

    public async Task<OAuthToken> SaveTokenAsync(OAuthToken token, CancellationToken cancellationToken = default)
    {
        var existingToken = await GetTokenAsync(token.UserMailAccountId, cancellationToken);
        if (existingToken != null)
        {
            existingToken.AccessToken = token.AccessToken;
            existingToken.RefreshToken = token.RefreshToken;
            existingToken.ExpiresAtUtc = token.ExpiresAtUtc;
            _context.OAuthTokens.Update(existingToken);
        }
        else
        {
            await _context.OAuthTokens.AddAsync(token, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return token;
    }
}
