using MailEngine.Core.Interfaces;

namespace MailEngine.Core.Models;

public class UserMailAccount
{
    public Guid Id { get; set; }
    public string EmailAddress { get; set; }
    public ProviderType ProviderType { get; set; }
}
