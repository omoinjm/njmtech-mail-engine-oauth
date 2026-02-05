using MailEngine.Core.Interfaces;
using MailEngine.Core.Models;
using MailEngine.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MailEngine.Infrastructure.Services;

public class DuplicateTracker : IDuplicateTracker
{
    private readonly MailEngineDbContext _dbContext;

    public DuplicateTracker(MailEngineDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> IsProcessedAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(messageId))
            return false;

        return await _dbContext.ProcessedMessages
            .AnyAsync(pm => pm.MessageId == messageId, cancellationToken);
    }

    public async Task<bool> IsProcessedByIdempotencyKeyAsync(string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(idempotencyKey))
            return false;

        return await _dbContext.ProcessedMessages
            .AnyAsync(pm => pm.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task MarkAsProcessedAsync(MailEvent mailEvent, CancellationToken cancellationToken = default)
    {
        var processedMessage = new ProcessedMessage
        {
            MessageId = mailEvent.MessageId.ToString(),
            IdempotencyKey = mailEvent.IdempotencyKey,
            EventType = mailEvent.GetType().Name,
            ProcessedAt = DateTime.UtcNow
        };

        _dbContext.ProcessedMessages.Add(processedMessage);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}