namespace MailEngine.Core.Models;

public class OAuthToken
{
    public Guid OAuthTokenId { get; set; }
    public Guid UserMailAccountId { get; set; }
    public required string AccessTokenTxt { get; set; }
    public required string RefreshTokenTxt { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
}
