using MailEngine.Core.Models;
using MailEngine.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace MailEngine.Functions.Services;

public interface IFailedMessageLogger
{
    Task LogFailedMessageAsync(
        string topic,
        string subscription,
        string messageContent,
        Exception exception,
        CancellationToken cancellationToken);
}

public class FailedMessageLogger : IFailedMessageLogger
{
    private readonly MailEngineDbContext _dbContext;
    private readonly ILogger<FailedMessageLogger> _logger;

    public FailedMessageLogger(MailEngineDbContext dbContext, ILogger<FailedMessageLogger> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Logs a failed message to the database for tracking and potential manual retry
    /// </summary>
    public async Task LogFailedMessageAsync(
        string topic,
        string subscription,
        string messageContent,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            var failedMessage = new FailedMessage
            {
                MessageIdTxt = Guid.NewGuid().ToString(),
                TopicCd = topic,
                SubscriptionTxt = subscription,
                ErrorMessageTxt = exception.Message,
                ErrorStackTraceTxt = exception.StackTrace ?? string.Empty,
                MessageContentTxt = messageContent,
                StatusCd = "in-dlq",
                RetryCountNo = 0,
                FailedAtUtc = DateTime.UtcNow
            };

            _dbContext.FailedMessages.Add(failedMessage);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Logged failed message to database. Topic: {Topic}, Subscription: {Subscription}, Error: {Error}",
                topic,
                subscription,
                exception.Message);
        }
        catch (Exception ex)
        {
            // If we can't log to database, at least log it to Application Insights
            _logger.LogError(ex, "Failed to log failed message to database. Original error: {OriginalError}", exception.Message);
        }
    }
}
