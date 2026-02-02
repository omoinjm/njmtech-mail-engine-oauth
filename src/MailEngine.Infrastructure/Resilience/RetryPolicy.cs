using Polly;
using Polly.Retry;

namespace MailEngine.Infrastructure.Resilience;

public static class CustomRetryPolicy
{
    public static AsyncRetryPolicy GetDefaultHttpRetryPolicy()
    {
        return Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}
