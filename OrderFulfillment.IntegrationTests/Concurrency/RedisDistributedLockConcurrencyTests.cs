using FluentAssertions;
using MediatR;
using OrderFulfillment.Application.Common.Behaviors;
using OrderFulfillment.Application.Interfaces;
using OrderFulfillment.Application.Orders.Commands.CreateOrder;
using OrderFulfillment.Domain.Entities;
using OrderFulfillment.Domain.ValueObjects;
using OrderFulfillment.Infrastructure.Services;
using StackExchange.Redis;
using Xunit;
using DomainOrder = OrderFulfillment.Domain.Entities.Order;

namespace OrderFulfillment.IntegrationTests.Concurrency;

public class RedisDistributedLockConcurrencyTests : IAsyncLifetime
{
    private const string RedisConnectionString = "localhost:6379";
    private readonly IConnectionMultiplexer _redis;
    private readonly List<string> _keysToCleanUp;

    public record TestConcurrencyCommand(string ResourceId) : IRequest<string>, ILockableRequest
    {
        public string LockKey => $"concurrency-test:{ResourceId}";
        public TimeSpan LockExpiration => TimeSpan.FromSeconds(5);
    }

    private sealed class PipelineExecutionResult
    {
        public bool Succeeded { get; init; }
        public string? Response { get; init; }
        public Exception? Exception { get; init; }
    }

    private sealed class ThreadSafeProductRepository : IProductRepository
    {
        private readonly Product _product;
        private readonly object _lock = new object();

        public ThreadSafeProductRepository(Product product)
        {
            _product = product;
        }

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return Task.FromResult<Product?>(_product);
            }
        }

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ThreadSafeOrderRepository : IOrderRepository
    {
        private readonly List<DomainOrder> _orders = new List<DomainOrder>();
        private readonly object _lock = new object();

        public IReadOnlyList<DomainOrder> Orders
        {
            get
            {
                lock (_lock)
                {
                    return _orders.ToList();
                }
            }
        }

        public Task<DomainOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                DomainOrder? order = _orders.FirstOrDefault((DomainOrder o) => o.Id == id);
                return Task.FromResult<DomainOrder?>(order);
            }
        }

        public Task AddAsync(DomainOrder order, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _orders.Add(order);
            }

            return Task.CompletedTask;
        }

        public Task UpdateAsync(DomainOrder order, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(1);
        }
    }

    public RedisDistributedLockConcurrencyTests()
    {
        _redis = ConnectionMultiplexer.Connect(RedisConnectionString);
        _keysToCleanUp = new List<string>();
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        IDatabase database = _redis.GetDatabase();

        foreach (string key in _keysToCleanUp)
        {
            await database.KeyDeleteAsync($"lock:{key}");
        }

        _redis.Dispose();
    }

    [Fact]
    public async Task AcquireLockAsync_When20TasksCompeteSimultaneously_OnlyOneAcquiresLockAndOthersAreRejected()
    {
        string resourceKey = "item-" + Guid.NewGuid().ToString("N");
        _keysToCleanUp.Add(resourceKey);

        int concurrencyLevel = 20;
        TaskCompletionSource<bool> startSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        List<Task<bool>> tasks = new List<Task<bool>>();

        for (int i = 0; i < concurrencyLevel; i++)
        {
            IDistributedLockService lockService = new RedisDistributedLockService(_redis);

            Task<bool> task = Task.Run(async () =>
            {
                await startSignal.Task;
                return await lockService.AcquireLockAsync(resourceKey, TimeSpan.FromSeconds(5));
            });

            tasks.Add(task);
        }

        startSignal.SetResult(true);
        bool[] results = await Task.WhenAll(tasks);

        int successCount = results.Count((bool acquired) => acquired);
        int failureCount = results.Count((bool acquired) => !acquired);

        successCount.Should().Be(1, "exactly one task must acquire the distributed lock");
        failureCount.Should().Be(19, "all other 19 concurrent tasks must be rejected to prevent race conditions");

        IDatabase database = _redis.GetDatabase();
        await database.KeyDeleteAsync($"lock:{resourceKey}");

        IDistributedLockService nextService = new RedisDistributedLockService(_redis);
        bool acquiredAfterRelease = await nextService.AcquireLockAsync(resourceKey, TimeSpan.FromSeconds(5));
        acquiredAfterRelease.Should().BeTrue("after the lock is released or deleted, subsequent requests must succeed");
    }

    [Fact]
    public async Task DistributedLockBehavior_WhenMultipleRequestsHitPipelineSimultaneously_ExecutesCriticalSectionStrictlyOnce()
    {
        string resourceKey = "pipeline-" + Guid.NewGuid().ToString("N");
        _keysToCleanUp.Add(resourceKey);

        TestConcurrencyCommand command = new TestConcurrencyCommand(resourceKey);
        int concurrencyLevel = 15;
        TaskCompletionSource<bool> startSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        int activeExecutions = 0;
        int peakConcurrentExecutions = 0;
        int completedExecutions = 0;
        object syncRoot = new object();

        List<Task<PipelineExecutionResult>> tasks = new List<Task<PipelineExecutionResult>>();

        for (int i = 0; i < concurrencyLevel; i++)
        {
            IDistributedLockService lockService = new RedisDistributedLockService(_redis);
            DistributedLockBehavior<TestConcurrencyCommand, string> behavior =
                new DistributedLockBehavior<TestConcurrencyCommand, string>(lockService);

            Task<PipelineExecutionResult> task = Task.Run(async () =>
            {
                await startSignal.Task;

                try
                {
                    string response = await behavior.Handle(
                        command,
                        async (CancellationToken cancellationToken) =>
                        {
                            lock (syncRoot)
                            {
                                activeExecutions++;
                                if (activeExecutions > peakConcurrentExecutions)
                                {
                                    peakConcurrentExecutions = activeExecutions;
                                }
                            }

                            await Task.Delay(100, cancellationToken);

                            lock (syncRoot)
                            {
                                activeExecutions--;
                                completedExecutions++;
                            }

                            return "OrderCreated";
                        },
                        CancellationToken.None);

                    return new PipelineExecutionResult
                    {
                        Succeeded = true,
                        Response = response
                    };
                }
                catch (Exception ex)
                {
                    return new PipelineExecutionResult
                    {
                        Succeeded = false,
                        Exception = ex
                    };
                }
            });

            tasks.Add(task);
        }

        startSignal.SetResult(true);
        PipelineExecutionResult[] results = await Task.WhenAll(tasks);

        int successfulRequests = results.Count((PipelineExecutionResult r) => r.Succeeded);
        int failedRequests = results.Count((PipelineExecutionResult r) => !r.Succeeded);

        peakConcurrentExecutions.Should().Be(1, "the critical section must NEVER have more than 1 active execution simultaneously");
        completedExecutions.Should().Be(1, "only 1 request must have completed the business operation");
        successfulRequests.Should().Be(1, "only 1 request in the MediatR pipeline must succeed");
        failedRequests.Should().Be(14, "all other 14 requests must be rejected by the DistributedLockBehavior");

        List<PipelineExecutionResult> failures = results.Where((PipelineExecutionResult r) => !r.Succeeded).ToList();
        foreach (PipelineExecutionResult failure in failures)
        {
            failure.Exception.Should().BeOfType<InvalidOperationException>();
            failure.Exception!.Message.Should().Contain("Could not acquire distributed lock");
        }
    }

    [Fact]
    public async Task AcquireLockAsync_WhenLockExpiresDueToTTL_AllowsSubsequentRequestToAcquireLockWithoutDeadlock()
    {
        string resourceKey = "ttl-" + Guid.NewGuid().ToString("N");
        _keysToCleanUp.Add(resourceKey);

        IDistributedLockService workerThatCrashes = new RedisDistributedLockService(_redis);
        IDistributedLockService healthyWorker = new RedisDistributedLockService(_redis);

        TimeSpan shortExpiration = TimeSpan.FromMilliseconds(400);

        bool worker1Acquired = await workerThatCrashes.AcquireLockAsync(resourceKey, shortExpiration);
        worker1Acquired.Should().BeTrue();

        bool worker2AcquiredImmediately = await healthyWorker.AcquireLockAsync(resourceKey, shortExpiration);
        worker2AcquiredImmediately.Should().BeFalse("Worker 1 still holds the active lock");

        await Task.Delay(550);

        bool worker2AcquiredAfterTtl = await healthyWorker.AcquireLockAsync(resourceKey, shortExpiration);

        worker2AcquiredAfterTtl.Should().BeTrue("Redis TTL must automatically release the abandoned lock to prevent deadlocks");
    }

    [Fact]
    public async Task CreateOrder_When10ConcurrentUsersTryToBuyTheLastItem_OnlyOneSucceedsAndStockNeverOversells()
    {
        Guid productId = Guid.NewGuid();
        Money price = new Money(799.99m, "USD");
        Product product = new Product(productId, "PlayStation 5 Pro", price, 1);

        string lockKey = $"product:{productId}";
        _keysToCleanUp.Add(lockKey);

        ThreadSafeProductRepository productRepo = new ThreadSafeProductRepository(product);
        ThreadSafeOrderRepository orderRepo = new ThreadSafeOrderRepository();
        FakeUnitOfWork unitOfWork = new FakeUnitOfWork();

        int concurrentBuyers = 10;
        TaskCompletionSource<bool> startSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        List<Task<PipelineExecutionResult>> buyerTasks = new List<Task<PipelineExecutionResult>>();

        for (int i = 0; i < concurrentBuyers; i++)
        {
            Guid buyerUserId = Guid.NewGuid();
            CreateOrderCommand command = new CreateOrderCommand(buyerUserId, productId, 1);

            IDistributedLockService lockService = new RedisDistributedLockService(_redis);
            DistributedLockBehavior<CreateOrderCommand, Guid> lockBehavior =
                new DistributedLockBehavior<CreateOrderCommand, Guid>(lockService);

            CreateOrderCommandHandler handler =
                new CreateOrderCommandHandler(orderRepo, productRepo, unitOfWork);

            Task<PipelineExecutionResult> task = Task.Run(async () =>
            {
                await startSignal.Task;

                try
                {
                    Guid orderId = await lockBehavior.Handle(
                        command,
                        (CancellationToken cancellationToken) => handler.Handle(command, cancellationToken),
                        CancellationToken.None);

                    return new PipelineExecutionResult
                    {
                        Succeeded = true,
                        Response = orderId.ToString()
                    };
                }
                catch (Exception ex)
                {
                    return new PipelineExecutionResult
                    {
                        Succeeded = false,
                        Exception = ex
                    };
                }
            });

            buyerTasks.Add(task);
        }

        startSignal.SetResult(true);
        PipelineExecutionResult[] results = await Task.WhenAll(buyerTasks);

        int successfulOrders = results.Count((PipelineExecutionResult r) => r.Succeeded);
        int failedOrders = results.Count((PipelineExecutionResult r) => !r.Succeeded);

        successfulOrders.Should().Be(1, "only 1 user can successfully buy the last available item");
        failedOrders.Should().Be(9, "all other 9 concurrent attempts must be rejected");

        orderRepo.Orders.Count.Should().Be(1, "exactly one order must be recorded in the repository");
        product.AvailableQuantity.Should().Be(0, "the product stock must be exactly 0, never negative (no overselling)");
    }
}
