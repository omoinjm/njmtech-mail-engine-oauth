using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using MailEngine.Core.Interfaces;
using MailEngine.Core.Models;
using MailEngine.Infrastructure.Data;
using MailEngine.Infrastructure.Services;
using Moq;
using Xunit;

namespace MailEngine.Tests.Unit;

public class DeduplicatedMailEventHandlerTests
{
    private readonly Mock<IMailEventDispatcher> _mockInnerHandler;
    private readonly Mock<IDuplicateTracker> _mockDuplicateTracker;
    private readonly Mock<ILogger<DeduplicatedMailEventHandler>> _mockLogger;
    private readonly DbContextOptions<MailEngineDbContext> _dbContextOptions;
    private MailEngineDbContext _dbContext;

    public DeduplicatedMailEventHandlerTests()
    {
        _mockInnerHandler = new Mock<IMailEventDispatcher>();
        _mockDuplicateTracker = new Mock<IDuplicateTracker>();
        _mockLogger = new Mock<ILogger<DeduplicatedMailEventHandler>>();

        _dbContextOptions = new DbContextOptionsBuilder<MailEngineDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique DB per test
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new MailEngineDbContext(_dbContextOptions);
    }

    [Fact]
    public async Task HandleEventAsync_WithNewMessage_ProcessesAndMarksAsProcessed()
    {
        // Arrange
        var handler = new DeduplicatedMailEventHandler(
            _mockInnerHandler.Object,
            _mockDuplicateTracker.Object,
            _dbContext,
            _mockLogger.Object);

        var mailEvent = new SendMailEvent
        {
            MessageId = Guid.NewGuid(),
            IdempotencyKey = "test-idempotency-key",
            To = "test@example.com",
            Subject = "Test Subject",
            Body = "Test Body"
        };

        _mockDuplicateTracker
            .Setup(dt => dt.IsProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockDuplicateTracker
            .Setup(dt => dt.IsProcessedByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await handler.HandleEventAsync(mailEvent);

        // Assert
        _mockInnerHandler.Verify(h => h.HandleEventAsync(mailEvent, It.IsAny<CancellationToken>()), Times.Once);
        _mockDuplicateTracker.Verify(dt => dt.MarkAsProcessedAsync(mailEvent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_WithDuplicateMessageId_SkipsProcessing()
    {
        // Arrange
        var handler = new DeduplicatedMailEventHandler(
            _mockInnerHandler.Object,
            _mockDuplicateTracker.Object,
            _dbContext,
            _mockLogger.Object);

        var mailEvent = new SendMailEvent
        {
            MessageId = Guid.NewGuid(),
            IdempotencyKey = "test-idempotency-key",
            To = "test@example.com",
            Subject = "Test Subject",
            Body = "Test Body"
        };

        _mockDuplicateTracker
            .Setup(dt => dt.IsProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Message already processed

        // Act
        await handler.HandleEventAsync(mailEvent);

        // Assert
        _mockInnerHandler.Verify(h => h.HandleEventAsync(mailEvent, It.IsAny<CancellationToken>()), Times.Never);
        _mockDuplicateTracker.Verify(dt => dt.MarkAsProcessedAsync(It.IsAny<MailEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleEventAsync_WithDuplicateIdempotencyKey_SkipsProcessing()
    {
        // Arrange
        var handler = new DeduplicatedMailEventHandler(
            _mockInnerHandler.Object,
            _mockDuplicateTracker.Object,
            _dbContext,
            _mockLogger.Object);

        var mailEvent = new SendMailEvent
        {
            MessageId = Guid.NewGuid(),
            IdempotencyKey = "test-idempotency-key",
            To = "test@example.com",
            Subject = "Test Subject",
            Body = "Test Body"
        };

        _mockDuplicateTracker
            .Setup(dt => dt.IsProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockDuplicateTracker
            .Setup(dt => dt.IsProcessedByIdempotencyKeyAsync("test-idempotency-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Idempotency key already processed

        // Act
        await handler.HandleEventAsync(mailEvent);

        // Assert
        _mockInnerHandler.Verify(h => h.HandleEventAsync(mailEvent, It.IsAny<CancellationToken>()), Times.Never);
        _mockDuplicateTracker.Verify(dt => dt.MarkAsProcessedAsync(It.IsAny<MailEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleEventAsync_WithNullIdempotencyKey_DoesNotCheckByIdempotencyKey()
    {
        // Arrange
        var handler = new DeduplicatedMailEventHandler(
            _mockInnerHandler.Object,
            _mockDuplicateTracker.Object,
            _dbContext,
            _mockLogger.Object);

        var mailEvent = new SendMailEvent
        {
            MessageId = Guid.NewGuid(),
            IdempotencyKey = null, // Null idempotency key
            To = "test@example.com",
            Subject = "Test Subject",
            Body = "Test Body"
        };

        _mockDuplicateTracker
            .Setup(dt => dt.IsProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await handler.HandleEventAsync(mailEvent);

        // Assert
        _mockInnerHandler.Verify(h => h.HandleEventAsync(mailEvent, It.IsAny<CancellationToken>()), Times.Once);
        _mockDuplicateTracker.Verify(dt => dt.IsProcessedByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockDuplicateTracker.Verify(dt => dt.MarkAsProcessedAsync(mailEvent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_WithEmptyIdempotencyKey_DoesNotCheckByIdempotencyKey()
    {
        // Arrange
        var handler = new DeduplicatedMailEventHandler(
            _mockInnerHandler.Object,
            _mockDuplicateTracker.Object,
            _dbContext,
            _mockLogger.Object);

        var mailEvent = new SendMailEvent
        {
            MessageId = Guid.NewGuid(),
            IdempotencyKey = "", // Empty idempotency key
            To = "test@example.com",
            Subject = "Test Subject",
            Body = "Test Body"
        };

        _mockDuplicateTracker
            .Setup(dt => dt.IsProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await handler.HandleEventAsync(mailEvent);

        // Assert
        _mockInnerHandler.Verify(h => h.HandleEventAsync(mailEvent, It.IsAny<CancellationToken>()), Times.Once);
        _mockDuplicateTracker.Verify(dt => dt.IsProcessedByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockDuplicateTracker.Verify(dt => dt.MarkAsProcessedAsync(mailEvent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_WithException_RollsbackTransaction()
    {
        // Arrange
        var handler = new DeduplicatedMailEventHandler(
            _mockInnerHandler.Object,
            _mockDuplicateTracker.Object,
            _dbContext,
            _mockLogger.Object);

        var mailEvent = new SendMailEvent
        {
            MessageId = Guid.NewGuid(),
            IdempotencyKey = "test-idempotency-key",
            To = "test@example.com",
            Subject = "Test Subject",
            Body = "Test Body"
        };

        _mockDuplicateTracker
            .Setup(dt => dt.IsProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockDuplicateTracker
            .Setup(dt => dt.IsProcessedByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockInnerHandler
            .Setup(h => h.HandleEventAsync(mailEvent, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => handler.HandleEventAsync(mailEvent));

        _mockInnerHandler.Verify(h => h.HandleEventAsync(mailEvent, It.IsAny<CancellationToken>()), Times.Once);
        _mockDuplicateTracker.Verify(dt => dt.MarkAsProcessedAsync(It.IsAny<MailEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}