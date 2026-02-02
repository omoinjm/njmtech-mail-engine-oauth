using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using MailEngine.Core.Interfaces;
using MailEngine.Core.Models;
using MailEngine.Functions.Dispatching;

namespace MailEngine.Tests.Unit;

[TestClass]
public class MailEventDispatcherTests
{
    private Mock<IMailProviderFactory>? _mockFactory;
    private Mock<IMailProvider>? _mockProvider;
    private ProviderConcurrencyLimiter? _concurrencyLimiter;
    private MailEventDispatcher? _dispatcher;

    [TestInitialize]
    public void Setup()
    {
        _mockFactory = new Mock<IMailProviderFactory>();
        _mockProvider = new Mock<IMailProvider>();
        _concurrencyLimiter = new ProviderConcurrencyLimiter(maxConcurrencyPerProvider: 10);
        _dispatcher = new MailEventDispatcher(_mockFactory.Object, _concurrencyLimiter);
    }

    [TestMethod]
    public async Task HandleEventAsync_SendMailEvent_CallsProviderSendEmailAsync()
    {
        var mailEvent = new SendMailEvent
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            ProviderType = ProviderType.Gmail,
            UserMailAccountId = Guid.NewGuid(),
            To = "test@example.com",
            Subject = "Test",
            Body = "Test body"
        };

        _mockFactory!.Setup(f => f.GetProvider(ProviderType.Gmail)).Returns(_mockProvider!.Object);
        _mockProvider!.Setup(p => p.SendEmailAsync(It.IsAny<SendMailEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _dispatcher!.HandleEventAsync(mailEvent);

        _mockProvider!.Verify(
            p => p.SendEmailAsync(It.Is<SendMailEvent>(e => e.MessageId == mailEvent.MessageId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleEventAsync_ReadInboxEvent_CallsProviderReadInboxAsync()
    {
        var mailEvent = new ReadInboxEvent
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            ProviderType = ProviderType.Outlook,
            UserMailAccountId = Guid.NewGuid()
        };

        _mockFactory!.Setup(f => f.GetProvider(ProviderType.Outlook)).Returns(_mockProvider!.Object);
        _mockProvider!.Setup(p => p.ReadInboxAsync(It.IsAny<ReadInboxEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _dispatcher!.HandleEventAsync(mailEvent);

        _mockProvider!.Verify(
            p => p.ReadInboxAsync(It.Is<ReadInboxEvent>(e => e.MessageId == mailEvent.MessageId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public async Task HandleEventAsync_UnsupportedEventType_ThrowsNotSupportedException()
    {
        var unsupportedEvent = new UnsupportedMailEvent
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            ProviderType = ProviderType.Gmail,
            UserMailAccountId = Guid.NewGuid()
        };

        _mockFactory!.Setup(f => f.GetProvider(ProviderType.Gmail)).Returns(_mockProvider!.Object);
        await _dispatcher!.HandleEventAsync(unsupportedEvent);
    }

    private class UnsupportedMailEvent : MailEvent { }
}
