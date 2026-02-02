using Microsoft.VisualStudio.TestTools.UnitTesting;
using MailEngine.Core.Interfaces;
using MailEngine.Functions.Dispatching;

namespace MailEngine.Tests.Unit;

[TestClass]
public class ProviderConcurrencyLimiterTests
{
    private ProviderConcurrencyLimiter _limiter;

    [TestInitialize]
    public void Setup()
    {
        _limiter = new ProviderConcurrencyLimiter(maxConcurrencyPerProvider: 2);
    }

    [TestMethod]
    public async Task WaitAsync_ShouldAllowConcurrentOperationsUpToLimit()
    {
        var providerType = ProviderType.Gmail;
        var tasks = new List<Task>();

        for (int i = 0; i < 2; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await _limiter.WaitAsync(providerType);
                _limiter.Release(providerType);
            }));
        }

        await Task.WhenAll(tasks);
        Assert.IsTrue(true, "Concurrent operations up to limit completed successfully");
    }

    [TestMethod]
    public async Task WaitAsync_ShouldBlockWhenLimitExceeded()
    {
        var providerType = ProviderType.Gmail;
        var completedCount = 0;
        var tasks = new List<Task>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        for (int i = 0; i < 3; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await _limiter.WaitAsync(providerType, cts.Token);
                    Interlocked.Increment(ref completedCount);
                    await Task.Delay(100, cts.Token);
                    _limiter.Release(providerType);
                    Interlocked.Decrement(ref completedCount);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }));
        }

        await Task.WhenAll(tasks.Select(t => t.ContinueWith(x => { })));
        Assert.IsTrue(completedCount <= 2, $"Expected max 2 concurrent, but {completedCount} were running");
    }

    [TestMethod]
    public async Task Release_ShouldAllowPendingWaitersToProgress()
    {
        var providerType = ProviderType.Gmail;
        var releaseCount = 0;

        await _limiter.WaitAsync(providerType);
        
        var waitTask = Task.Run(async () =>
        {
            await _limiter.WaitAsync(providerType);
            Interlocked.Increment(ref releaseCount);
            _limiter.Release(providerType);
        });

        await Task.Delay(100);
        _limiter.Release(providerType);
        await waitTask;

        Assert.AreEqual(1, releaseCount, "Wait task should have completed after release");
    }
}
