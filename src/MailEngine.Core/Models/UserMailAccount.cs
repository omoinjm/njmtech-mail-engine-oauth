using MailEngine.Core.Interfaces;

namespace MailEngine.Core.Models;

public class UserMailAccount
{
    public Guid UserMailAccountId { get; set; }
    public required string EmailAddressTxt { get; set; }
    public int ProviderCd { get; set; } // 0=Gmail, 1=Outlook
    public bool IsActiveFalg { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
}
