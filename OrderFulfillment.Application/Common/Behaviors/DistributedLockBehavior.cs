using MediatR;
using OrderFulfillment.Application.Common.Exceptions;
using OrderFulfillment.Application.Interfaces;

namespace OrderFulfillment.Application.Common.Behaviors;

public class DistributedLockBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IDistributedLockService _lockService;

    public DistributedLockBehavior(IDistributedLockService lockService)
    {
        _lockService = lockService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ILockableRequest lockableRequest)
        {
            return await next();
        }

        string lockKey = lockableRequest.LockKey;
        bool lockAcquired = await _lockService.AcquireLockAsync(lockKey, lockableRequest.LockExpiration, cancellationToken);

        if (!lockAcquired)
        {
            throw new DistributedLockException(lockKey);
        }

        try
        {
            return await next();
        }
        finally
        {
            await _lockService.ReleaseLockAsync(lockKey, cancellationToken);
        }
    }
}