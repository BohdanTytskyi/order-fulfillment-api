using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderFulfillment.Application.Common.Exceptions;
using OrderFulfillment.Application.Orders.Queries.GetOrderById;
using OrderFulfillment.Domain.Entities;
using OrderFulfillment.Domain.ValueObjects;
using OrderFulfillment.Infrastructure.Data;
using Xunit;

namespace OrderFulfillment.UnitTests.Application;

public class GetOrderByIdQueryHandlerTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_WhenOrderExists_ShouldReturnOrderResponseDtoWithCorrectDetails()
    {
        using ApplicationDbContext context = CreateInMemoryDbContext();

        Guid orderId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();
        Money price = new Money(120m, "USD");

        Order order = new Order(orderId, userId, discountPercentage: 10m);
        order.AddItem(productId, price, quantity: 2);
        order.Confirm();

        await context.Orders.AddAsync(order);
        await context.SaveChangesAsync();

        GetOrderByIdQueryHandler handler = new GetOrderByIdQueryHandler(context);
        GetOrderByIdQuery query = new GetOrderByIdQuery(orderId);

        OrderResponseDto result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(orderId);
        result.UserId.Should().Be(userId);
        result.Status.Should().Be(OrderStatus.Confirmed.ToString());
        result.DiscountPercentage.Should().Be(10m);
        result.Currency.Should().Be("USD");
        result.TotalAmount.Should().Be(216m);
        result.Items.Should().HaveCount(1);

        OrderItemResponseDto itemDto = result.Items[0];
        itemDto.ProductId.Should().Be(productId);
        itemDto.UnitPrice.Should().Be(120m);
        itemDto.Quantity.Should().Be(2);
        itemDto.TotalPrice.Should().Be(240m);
    }

    [Fact]
    public async Task Handle_WhenOrderDoesNotExist_ShouldThrowOrderNotFoundException()
    {
        using ApplicationDbContext context = CreateInMemoryDbContext();
        GetOrderByIdQueryHandler handler = new GetOrderByIdQueryHandler(context);

        Guid nonExistentOrderId = Guid.NewGuid();
        GetOrderByIdQuery query = new GetOrderByIdQuery(nonExistentOrderId);

        Func<Task> act = async () => await handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<OrderNotFoundException>()
            .WithMessage($"Order '{nonExistentOrderId}' not found.");
    }
}
