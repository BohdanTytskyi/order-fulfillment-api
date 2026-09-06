namespace OrderFulfillment.Application.Common.Behaviors;

public interface ILockableRequest
{
    string LockKey { get; }
    TimeSpan LockExpiration => TimeSpan.FromSeconds(5);
}