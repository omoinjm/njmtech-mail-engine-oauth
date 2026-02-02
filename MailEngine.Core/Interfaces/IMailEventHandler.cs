using MailEngine.Core.Models;

namespace MailEngine.Core.Interfaces;

public interface IMailEventHandler
{
    Task HandleEventAsync(MailEvent mailEvent, CancellationToken cancellationToken = default);
}
