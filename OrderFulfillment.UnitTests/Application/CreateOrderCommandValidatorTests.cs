using FluentAssertions;
using FluentValidation.Results;
using OrderFulfillment.Application.Orders.Commands.CreateOrder;
using Xunit;

namespace OrderFulfillment.UnitTests.Application;

public class CreateOrderCommandValidatorTests
{
    private readonly CreateOrderCommandValidator _validator;

    public CreateOrderCommandValidatorTests()
    {
        _validator = new CreateOrderCommandValidator();
    }

    [Fact]
    public void Validate_WhenCommandIsValid_ShouldNotHaveValidationErrors()
    {
        CreateOrderCommand command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), 5);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationErrorForUserId()
    {
        CreateOrderCommand command = new CreateOrderCommand(Guid.Empty, Guid.NewGuid(), 5);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateOrderCommand.UserId));
        result.Errors[0].ErrorMessage.Should().Be("UserId is required.");
    }

    [Fact]
    public void Validate_WhenProductIdIsEmpty_ShouldHaveValidationErrorForProductId()
    {
        CreateOrderCommand command = new CreateOrderCommand(Guid.NewGuid(), Guid.Empty, 5);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateOrderCommand.ProductId));
        result.Errors[0].ErrorMessage.Should().Be("ProductId is required.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_WhenQuantityIsZeroOrNegative_ShouldHaveValidationErrorForQuantity(int invalidQuantity)
    {
        CreateOrderCommand command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), invalidQuantity);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateOrderCommand.Quantity));
        result.Errors[0].ErrorMessage.Should().Be("Quantity must be greater than zero.");
    }
}
