namespace MailEngine.Core.Models;

public class SendMailEvent : MailEvent
{
    public required string To { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
}
