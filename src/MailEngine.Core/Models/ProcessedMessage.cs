using System.ComponentModel.DataAnnotations;

namespace MailEngine.Core.Models;

public class ProcessedMessage
{
    [Key]
    public Guid ProcessedMessageId { get; set; } = Guid.NewGuid();
    
    public required string MessageId { get; set; }
    
    public string? IdempotencyKey { get; set; }
    
    public required string EventType { get; set; }
    
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}