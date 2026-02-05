using MailEngine.Core.Models;

namespace MailEngine.Core.Interfaces;

public interface IDuplicateTracker
{
    Task<bool> IsProcessedAsync(string messageId, CancellationToken cancellationToken = default);
    Task<bool> IsProcessedByIdempotencyKeyAsync(string? idempotencyKey, CancellationToken cancellationToken = default);
    Task MarkAsProcessedAsync(MailEvent mailEvent, CancellationToken cancellationToken = default);
}