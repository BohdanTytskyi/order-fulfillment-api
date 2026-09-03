using FluentAssertions;
using OrderFulfillment.Domain.Entities;
using OrderFulfillment.Domain.Events;
using OrderFulfillment.Domain.ValueObjects;
using Xunit;

namespace OrderFulfillment.UnitTests.Domain;

public class OrderTests
{
    [Fact]
    public void AddItem_WhenItemsAdded_ShouldCalculateTotalAmountCorrectly()
    {
        Order order = new Order(Guid.NewGuid(), Guid.NewGuid(), discountPercentage: 0);
        Money itemPrice = new Money(100m, "USD");

        order.AddItem(Guid.NewGuid(), itemPrice, 2);
        order.AddItem(Guid.NewGuid(), itemPrice, 1);

        Money total = order.CalculateTotal();

        total.Amount.Should().Be(300m);
        total.Currency.Should().Be("USD");
        order.Items.Should().HaveCount(2);
    }

    [Fact]
    public void CalculateTotal_WhenDiscountIsApplied_ShouldDeductDiscountFromTotal()
    {
        Order order = new Order(Guid.NewGuid(), Guid.NewGuid(), discountPercentage: 10);
        Money itemPrice = new Money(100m, "USD");

        order.AddItem(Guid.NewGuid(), itemPrice, 2);

        Money total = order.CalculateTotal();

        total.Amount.Should().Be(180m);
    }

    [Fact]
    public void Confirm_WhenOrderHasItems_ShouldSetStatusToConfirmedAndRaiseOrderCreatedEvent()
    {
        Order order = new Order(Guid.NewGuid(), Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), new Money(50m, "USD"), 1);

        order.Confirm();

        order.Status.Should().Be(OrderStatus.Confirmed);
        order.DomainEvents.Should().ContainSingle();
        order.DomainEvents.First().Should().BeOfType<OrderCreatedEvent>();

        OrderCreatedEvent createdEvent = (OrderCreatedEvent)order.DomainEvents.First();
        createdEvent.OrderId.Should().Be(order.Id);
        createdEvent.TotalAmount.Amount.Should().Be(50m);
    }

    [Fact]
    public void Confirm_WhenOrderIsEmpty_ShouldThrowInvalidOperationException()
    {
        Order emptyOrder = new Order(Guid.NewGuid(), Guid.NewGuid());

        Action act = () => emptyOrder.Confirm();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot confirm an empty order.");
    }
}