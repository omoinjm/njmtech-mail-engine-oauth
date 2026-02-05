using MailEngine.Core.Interfaces;
using MailEngine.Core.Models;
using MailEngine.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MailEngine.Infrastructure.Services;

public class DeduplicatedMailEventHandler : IMailEventHandler
{
    private readonly IMailEventDispatcher _innerHandler;
    private readonly IDuplicateTracker _duplicateTracker;
    private readonly MailEngineDbContext _dbContext;
    private readonly ILogger<DeduplicatedMailEventHandler> _logger;

    public DeduplicatedMailEventHandler(
        IMailEventDispatcher innerHandler,
        IDuplicateTracker duplicateTracker,
        MailEngineDbContext dbContext,
        ILogger<DeduplicatedMailEventHandler> logger)
    {
        _innerHandler = innerHandler;
        _duplicateTracker = duplicateTracker;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task HandleEventAsync(MailEvent mailEvent, CancellationToken cancellationToken = default)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Check if message has already been processed using MessageId
            if (await _duplicateTracker.IsProcessedAsync(mailEvent.MessageId.ToString(), cancellationToken))
            {
                _logger.LogInformation("Duplicate message detected and skipped. MessageId: {MessageId}, EventType: {EventType}", 
                    mailEvent.MessageId, mailEvent.GetType().Name);
                
                await transaction.CommitAsync(cancellationToken);
                return; // Skip duplicate
            }

            // Also check by idempotency key if present
            if (!string.IsNullOrEmpty(mailEvent.IdempotencyKey))
            {
                if (await _duplicateTracker.IsProcessedByIdempotencyKeyAsync(mailEvent.IdempotencyKey, cancellationToken))
                {
                    _logger.LogInformation("Duplicate message detected by idempotency key and skipped. IdempotencyKey: {IdempotencyKey}, EventType: {EventType}", 
                        mailEvent.IdempotencyKey, mailEvent.GetType().Name);
                    
                    await transaction.CommitAsync(cancellationToken);
                    return; // Skip duplicate
                }
            }

            // Process the message
            await _innerHandler.HandleEventAsync(mailEvent, cancellationToken);

            // Record that message was processed
            await _duplicateTracker.MarkAsProcessedAsync(mailEvent, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            
            _logger.LogInformation("Message processed successfully. MessageId: {MessageId}, EventType: {EventType}", 
                mailEvent.MessageId, mailEvent.GetType().Name);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error processing message. MessageId: {MessageId}, EventType: {EventType}", 
                mailEvent.MessageId, mailEvent.GetType().Name);
            throw;
        }
    }
}