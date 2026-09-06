using OrderFulfillment.Application.Interfaces;
using StackExchange.Redis;

namespace OrderFulfillment.Infrastructure.Services;

public class RedisDistributedLockService : IDistributedLockService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _lockValue;

    public RedisDistributedLockService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _lockValue = Environment.MachineName + "_" + Guid.NewGuid().ToString("N");
    }

    public async Task<bool> AcquireLockAsync(string resourceKey, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        IDatabase database = _redis.GetDatabase();
        RedisKey key = $"lock:{resourceKey}";
        RedisValue value = _lockValue;

        return await database.LockTakeAsync(key, value, expiration);
    }

    public async Task ReleaseLockAsync(string resourceKey, CancellationToken cancellationToken = default)
    {
        IDatabase database = _redis.GetDatabase();
        RedisKey key = $"lock:{resourceKey}";
        RedisValue value = _lockValue;

        await database.LockReleaseAsync(key, value);
    }
}