using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MailEngine.Infrastructure.Data;
using MailEngine.Core.Models;

namespace MailEngine.Functions.Functions;

public class MonitorDLQFunction
{
    private readonly MailEngineDbContext _dbContext;
    private readonly ILogger<MonitorDLQFunction> _logger;

    public MonitorDLQFunction(MailEngineDbContext dbContext, ILogger<MonitorDLQFunction> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Monitors the Dead Letter Queue for the mail-send topic and logs failures
    /// Runs every 5 minutes to check for failed messages
    /// </summary>
    [Function("MonitorDLQFunction")]
    public async Task RunAsync(
        [TimerTrigger("0 */5 * * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("DLQ Monitor: Starting check. NextExecution: {NextExecution}", timerInfo.ScheduleStatus.Next);

            // Count messages currently in DLQ (in database)
            var dlqMessages = _dbContext.FailedMessages
                .Where(f => f.StatusCd == "in-dlq")
                .ToList();

            if (dlqMessages.Count > 0)
            {
                _logger.LogError("DLQ ALERT: {Count} messages in Dead Letter Queue requiring investigation", dlqMessages.Count);

                // Log summary of failures by topic
                var byTopic = dlqMessages.GroupBy(m => m.TopicCd);
                foreach (var topicGroup in byTopic)
                {
                    _logger.LogError("  Topic: {Topic} - {Count} failed messages", topicGroup.Key, topicGroup.Count());

                    // Log the most recent failure for each topic
                    var mostRecent = topicGroup.OrderByDescending(m => m.FailedAtUtc).First();
                    _logger.LogError("    Most recent: {ErrorMessage} at {FailedAt}",
                        mostRecent.ErrorMessageTxt,
                        mostRecent.FailedAtUtc);
                }

                // TODO: Integration point - Send alert to Slack/Teams/PagerDuty
                // Example:
                // await _slackNotificationService.SendAlert(
                //     $"🚨 Mail Engine DLQ Alert: {dlqMessages.Count} messages failed",
                //     dlqMessages);
            }
            else
            {
                _logger.LogInformation("DLQ Monitor: No messages in Dead Letter Queue. System healthy.");
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DLQ Monitor failed with exception");
            throw;
        }
    }
}
