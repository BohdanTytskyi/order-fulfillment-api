namespace OrderFulfillment.Application.Interfaces;

public interface IDistributedLockService
{
    Task<bool> AcquireLockAsync(string resourceKey, TimeSpan expiration, CancellationToken cancellationToken = default);
    Task ReleaseLockAsync(string resourceKey, CancellationToken cancellationToken = default);
}