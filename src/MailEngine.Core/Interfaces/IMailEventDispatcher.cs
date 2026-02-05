using MailEngine.Core.Models;

namespace MailEngine.Core.Interfaces;

public interface IMailEventDispatcher : IMailEventHandler
{
    // Inherits HandleEventAsync from IMailEventHandler
}