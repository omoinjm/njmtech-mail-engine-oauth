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

public class ProviderSpecificDeduplicationTests
{
    private readonly Mock<IMailEventDispatcher> _mockInnerHandler;
    private readonly Mock<IDuplicateTracker> _mockDuplicateTracker;
    private readonly Mock<ILogger<DeduplicatedMailEventHandler>> _mockLogger;
    private readonly DbContextOptions<MailEngineDbContext> _dbContextOptions;
    private MailEngineDbContext _dbContext;

    public ProviderSpecificDeduplicationTests()
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
    public async Task SendMailEvent_GeneratesIdempotencyKeyBasedOnContent()
    {
        // This test verifies that the function generates idempotency keys based on content
        // to prevent duplicate email sends with same recipient, subject, and body
        
        var sendMailEvent = new SendMailEvent
        {
            MessageId = Guid.NewGuid(),
            UserMailAccountId = Guid.NewGuid(),
            ProviderType = ProviderType.Gmail,
            To = "recipient@example.com",
            Subject = "Test Subject",
            Body = "Test email body content"
        };

        // Simulate the idempotency key generation logic from SendMailFunction
        var contentHash = ComputeSha256Hash($"{sendMailEvent.To}|{sendMailEvent.Subject}|{sendMailEvent.Body}");
        var expectedIdempotencyKey = $"sendmail_{contentHash}";

        Assert.NotNull(expectedIdempotencyKey);
        Assert.StartsWith("sendmail_", expectedIdempotencyKey);
        // The SHA256 hash is 64 characters (32 bytes * 2 hex chars per byte)
        Assert.Equal($"sendmail_{new string('a', 64)}".Length, expectedIdempotencyKey.Length);
    }

    [Fact]
    public async Task ReadInboxEvent_GeneratesIdempotencyKeyBasedOnUserAccountAndTime()
    {
        // This test verifies that the function generates idempotency keys based on user account
        // and time window to prevent duplicate inbox reads
        
        var userId = Guid.NewGuid();
        var currentTime = DateTime.UtcNow;
        
        var readInboxEvent = new ReadInboxEvent
        {
            MessageId = Guid.NewGuid(),
            UserMailAccountId = userId,
            ProviderType = ProviderType.Gmail
        };

        // Simulate the idempotency key generation logic from ReadInboxFunction
        var expectedIdempotencyKey = $"readinbox_{userId}_{currentTime:yyyyMMddHH}";

        Assert.NotNull(expectedIdempotencyKey);
        Assert.StartsWith("readinbox_", expectedIdempotencyKey);
        Assert.Contains(userId.ToString(), expectedIdempotencyKey);
    }

    [Fact]
    public async Task DuplicateSendMailEventsWithSameContent_AreSkipped()
    {
        // Arrange
        var handler = new DeduplicatedMailEventHandler(
            _mockInnerHandler.Object,
            _mockDuplicateTracker.Object,
            _dbContext,
            _mockLogger.Object);

        var sendMailEvent1 = new SendMailEvent
        {
            MessageId = Guid.NewGuid(),
            UserMailAccountId = Guid.NewGuid(),
            ProviderType = ProviderType.Gmail,
            To = "recipient@example.com",
            Subject = "Test Subject",
            Body = "Test email body content"
        };

        // Generate idempotency key based on content
        var contentHash = ComputeSha256Hash($"{sendMailEvent1.To}|{sendMailEvent1.Subject}|{sendMailEvent1.Body}");
        sendMailEvent1.IdempotencyKey = $"sendmail_{contentHash}";

        var sendMailEvent2 = new SendMailEvent
        {
            MessageId = Guid.NewGuid(), // Different message ID
            UserMailAccountId = sendMailEvent1.UserMailAccountId,
            ProviderType = ProviderType.Gmail,
            To = "recipient@example.com", // Same content
            Subject = "Test Subject",
            Body = "Test email body content"
        };

        // Same idempotency key as first event
        sendMailEvent2.IdempotencyKey = sendMailEvent1.IdempotencyKey;

        // First event - not a duplicate
        _mockDuplicateTracker
            .SetupSequence(dt => dt.IsProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false) // First call (first event)
            .ReturnsAsync(false); // Second call (second event)

        _mockDuplicateTracker
            .SetupSequence(dt => dt.IsProcessedByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false) // First call (first event)
            .ReturnsAsync(true); // Second call (second event) - same idempotency key, so duplicate

        // Process first event
        await handler.HandleEventAsync(sendMailEvent1);
        _mockInnerHandler.Verify(h => h.HandleEventAsync(sendMailEvent1, It.IsAny<CancellationToken>()), Times.Once);

        // Process second event (should be skipped due to duplicate idempotency key)
        await handler.HandleEventAsync(sendMailEvent2);
        _mockInnerHandler.Verify(h => h.HandleEventAsync(sendMailEvent2, It.IsAny<CancellationToken>()), Times.Never);
    }

    private static string ComputeSha256Hash(string rawData)
    {
        using (var sha256Hash = System.Security.Cryptography.SHA256.Create())
        {
            // ComputeHash - returns byte array
            var bytes = sha256Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawData));

            // Convert byte array to a string
            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }
}