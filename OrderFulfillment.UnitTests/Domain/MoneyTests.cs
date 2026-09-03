using FluentAssertions;
using OrderFulfillment.Domain.ValueObjects;
using Xunit;

namespace OrderFulfillment.UnitTests.Domain;

public class MoneyTests
{
    [Fact]
    public void Add_WhenCurrenciesMatch_ShouldReturnCorrectSum()
    {
        Money firstMoney = new Money(100.50m, "USD");
        Money secondMoney = new Money(49.50m, "USD");

        Money result = firstMoney + secondMoney;

        result.Amount.Should().Be(150.00m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Add_WhenCurrenciesDoNotMatch_ShouldThrowInvalidOperationException()
    {
        Money usd = new Money(100m, "USD");
        Money eur = new Money(50m, "EUR");

        Action act = () => { Money unused = usd + eur; };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot add EUR to USD");
    }

    [Fact]
    public void Subtract_WhenResultWouldBeNegative_ShouldThrowInvalidOperationException()
    {
        Money wallet = new Money(50m, "USD");
        Money cost = new Money(100m, "USD");

        Action act = () => { Money unused = wallet - cost; };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Resulting money amount cannot be negative.");
    }

    [Fact]
    public void ApplyDiscount_WhenValidPercentageProvided_ShouldCalculateDiscountedAmount()
    {
        Money originalPrice = new Money(200m, "USD");
        decimal discountPercentage = 15m;

        Money discountedPrice = originalPrice.ApplyDiscount(discountPercentage);

        discountedPrice.Amount.Should().Be(170m);
        discountedPrice.Currency.Should().Be("USD");
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(105)]
    public void ApplyDiscount_WhenPercentageOutOfRange_ShouldThrowArgumentException(decimal invalidPercentage)
    {
        Money price = new Money(100m, "USD");

        Action act = () => price.ApplyDiscount(invalidPercentage);

        act.Should().Throw<ArgumentException>();
    }
}