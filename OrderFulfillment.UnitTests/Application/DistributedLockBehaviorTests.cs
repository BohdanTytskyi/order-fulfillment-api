using FluentAssertions;
using MediatR;
using NSubstitute;
using OrderFulfillment.Application.Common.Behaviors;
using OrderFulfillment.Application.Interfaces;
using Xunit;

namespace OrderFulfillment.UnitTests.Application;

public class DistributedLockBehaviorTests
{
    private readonly IDistributedLockService _lockService;
    private readonly DistributedLockBehavior<TestLockableCommand, string> _behavior;

    public record TestLockableCommand(string ResourceId) : IRequest<string>, ILockableRequest
    {
        public string LockKey => $"test:{ResourceId}";
        public TimeSpan LockExpiration => TimeSpan.FromSeconds(2);
    }

    public record TestNonLockableCommand : IRequest<string>;

    public DistributedLockBehaviorTests()
    {
        _lockService = Substitute.For<IDistributedLockService>();
        _behavior = new DistributedLockBehavior<TestLockableCommand, string>(_lockService);
    }

    [Fact]
    public async Task Handle_WhenLockIsAcquired_ShouldExecuteNextDelegateAndReleaseLock()
    {
        TestLockableCommand command = new TestLockableCommand("item-123");
        string expectedResult = "Success";
        bool nextWasCalled = false;

        _lockService.AcquireLockAsync(command.LockKey, command.LockExpiration, Arg.Any<CancellationToken>())
            .Returns(true);

        RequestHandlerDelegate<string> next = (cancellationToken) =>
        {
            nextWasCalled = true;
            return Task.FromResult(expectedResult);
        };

        string result = await _behavior.Handle(command, next, CancellationToken.None);

        result.Should().Be(expectedResult);
        nextWasCalled.Should().BeTrue();

        await _lockService.Received(1).AcquireLockAsync(command.LockKey, command.LockExpiration, Arg.Any<CancellationToken>());
        await _lockService.Received(1).ReleaseLockAsync(command.LockKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenLockFailsToAcquire_ShouldThrowInvalidOperationExceptionAndNotCallNext()
    {
        TestLockableCommand command = new TestLockableCommand("item-123");
        bool nextWasCalled = false;

        _lockService.AcquireLockAsync(command.LockKey, command.LockExpiration, Arg.Any<CancellationToken>())
            .Returns(false);

        RequestHandlerDelegate<string> next = (cancellationToken) =>
        {
            nextWasCalled = true;
            return Task.FromResult("Should not happen");
        };

        Func<Task> act = async () => await _behavior.Handle(command, next, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Could not acquire distributed lock for key '{command.LockKey}'*");

        nextWasCalled.Should().BeFalse();
        await _lockService.DidNotReceive().ReleaseLockAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRequestIsNotLockable_ShouldPassThroughDirectlyWithoutTouchingLockService()
    {
        DistributedLockBehavior<TestNonLockableCommand, string> nonLockableBehavior =
            new DistributedLockBehavior<TestNonLockableCommand, string>(_lockService);

        TestNonLockableCommand nonLockableCommand = new TestNonLockableCommand();
        bool nextWasCalled = false;

        RequestHandlerDelegate<string> next = (cancellationToken) =>
        {
            nextWasCalled = true;
            return Task.FromResult("Non-locked response");
        };

        string result = await nonLockableBehavior.Handle(nonLockableCommand, next, CancellationToken.None);

        result.Should().Be("Non-locked response");
        nextWasCalled.Should().BeTrue();
        await _lockService.DidNotReceive().AcquireLockAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }
}