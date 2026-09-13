using FluentAssertions;
using OrderFulfillment.Domain.Entities;
using OrderFulfillment.Domain.Exceptions;
using OrderFulfillment.Domain.ValueObjects;
using Xunit;

namespace OrderFulfillment.UnitTests.Domain;

public class ProductTests
{
    [Fact]
    public void Reserve_WhenStockIsSufficient_ShouldDecreaseAvailableQuantity()
    {
        Product product = new Product(Guid.NewGuid(), "PlayStation 5", new Money(500m, "USD"), 10);

        product.Reserve(3);

        product.AvailableQuantity.Should().Be(7);
    }

    [Fact]
    public void Reserve_WhenRequestedQuantityExceedsAvailableStock_ShouldThrowInsufficientStockException()
    {
        Product product = new Product(Guid.NewGuid(), "PlayStation 5", new Money(500m, "USD"), 2);

        Action act = () => product.Reserve(5);

        act.Should().Throw<InsufficientStockException>()
            .WithMessage("*Not enough stock*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Reserve_WhenQuantityIsZeroOrNegative_ShouldThrowArgumentException(int invalidQuantity)
    {
        Product product = new Product(Guid.NewGuid(), "PlayStation 5", new Money(500m, "USD"), 10);

        Action act = () => product.Reserve(invalidQuantity);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Restock_WhenQuantityIsValid_ShouldIncreaseAvailableQuantity()
    {
        Product product = new Product(Guid.NewGuid(), "PlayStation 5", new Money(500m, "USD"), 5);

        product.Restock(5);

        product.AvailableQuantity.Should().Be(10);
    }
}