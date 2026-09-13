namespace OrderFulfillment.Application.Common.Exceptions;

public class DistributedLockException : Exception
{
    public string LockKey { get; }

    public DistributedLockException(string lockKey)
        : base($"Could not acquire distributed lock for key '{lockKey}'. Resource is currently busy.")
    {
        LockKey = lockKey;
    }
}
