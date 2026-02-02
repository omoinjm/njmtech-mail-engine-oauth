namespace MailEngine.Core.Models;

public class OAuthToken
{
    public Guid Id { get; set; }
    public Guid UserMailAccountId { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
