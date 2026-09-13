using FluentAssertions;
using FluentValidation;
using MediatR;
using OrderFulfillment.Application.Common.Behaviors;
using OrderFulfillment.Application.Orders.Commands.CreateOrder;
using ValidationException = OrderFulfillment.Application.Common.Exceptions.ValidationException;
using Xunit;

namespace OrderFulfillment.UnitTests.Application;

public class ValidationBehaviorTests
{
    private class SuccessfulValidator : AbstractValidator<CreateOrderCommand>
    {
    }

    private class FailingValidator : AbstractValidator<CreateOrderCommand>
    {
        public FailingValidator()
        {
            RuleFor(x => x.Quantity)
                .GreaterThan(0)
                .WithMessage("Quantity must be greater than zero.");
        }
    }

    [Fact]
    public async Task Handle_WhenNoValidatorsExist_ShouldCallNext()
    {
        List<IValidator<CreateOrderCommand>> validators = new List<IValidator<CreateOrderCommand>>();
        ValidationBehavior<CreateOrderCommand, Guid> behavior = new ValidationBehavior<CreateOrderCommand, Guid>(validators);

        CreateOrderCommand command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), 1);
        bool nextWasCalled = false;
        RequestHandlerDelegate<Guid> next = (cancellationToken) =>
        {
            nextWasCalled = true;
            return Task.FromResult(Guid.NewGuid());
        };

        Guid result = await behavior.Handle(command, next, CancellationToken.None);

        nextWasCalled.Should().BeTrue();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WhenValidationSucceeds_ShouldCallNext()
    {
        List<IValidator<CreateOrderCommand>> validators = new List<IValidator<CreateOrderCommand>>
        {
            new SuccessfulValidator()
        };
        ValidationBehavior<CreateOrderCommand, Guid> behavior = new ValidationBehavior<CreateOrderCommand, Guid>(validators);

        CreateOrderCommand command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), 1);
        bool nextWasCalled = false;
        RequestHandlerDelegate<Guid> next = (cancellationToken) =>
        {
            nextWasCalled = true;
            return Task.FromResult(Guid.NewGuid());
        };

        Guid result = await behavior.Handle(command, next, CancellationToken.None);

        nextWasCalled.Should().BeTrue();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WhenValidationFails_ShouldThrowValidationExceptionAndNotCallNext()
    {
        List<IValidator<CreateOrderCommand>> validators = new List<IValidator<CreateOrderCommand>>
        {
            new FailingValidator()
        };
        ValidationBehavior<CreateOrderCommand, Guid> behavior = new ValidationBehavior<CreateOrderCommand, Guid>(validators);

        CreateOrderCommand invalidCommand = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), 0);
        bool nextWasCalled = false;
        RequestHandlerDelegate<Guid> next = (cancellationToken) =>
        {
            nextWasCalled = true;
            return Task.FromResult(Guid.NewGuid());
        };

        Func<Task> act = async () => await behavior.Handle(invalidCommand, next, CancellationToken.None);

        FluentAssertions.Specialized.ExceptionAssertions<ValidationException> exceptionAssertion =
            await act.Should().ThrowAsync<ValidationException>();

        exceptionAssertion.Which.Errors.Should().ContainKey(nameof(CreateOrderCommand.Quantity));
        exceptionAssertion.Which.Errors[nameof(CreateOrderCommand.Quantity)].Should().Contain("Quantity must be greater than zero.");
        nextWasCalled.Should().BeFalse();
    }
}
