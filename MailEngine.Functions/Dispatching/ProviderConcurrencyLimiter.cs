using System.Collections.Concurrent;
using MailEngine.Core.Interfaces;

namespace MailEngine.Functions.Dispatching;

public class ProviderConcurrencyLimiter
{
    private readonly ConcurrentDictionary<ProviderType, SemaphoreSlim> _semaphores;

    public ProviderConcurrencyLimiter(int maxConcurrencyPerProvider)
    {
        _semaphores = new ConcurrentDictionary<ProviderType, SemaphoreSlim>();
        foreach (ProviderType providerType in Enum.GetValues(typeof(ProviderType)))
        {
            _semaphores.TryAdd(providerType, new SemaphoreSlim(maxConcurrencyPerProvider, maxConcurrencyPerProvider));
        }
    }

    public async Task WaitAsync(ProviderType providerType, CancellationToken cancellationToken = default)
    {
        if (_semaphores.TryGetValue(providerType, out var semaphore))
        {
            await semaphore.WaitAsync(cancellationToken);
        }
        else
        {
            throw new InvalidOperationException($"No semaphore found for provider type {providerType}");
        }
    }

    public void Release(ProviderType providerType)
    {
        if (_semaphores.TryGetValue(providerType, out var semaphore))
        {
            semaphore.Release();
        }
    }
}
