using FluentAssertions;
using NSubstitute;
using OrderFulfillment.Application.Common.Exceptions;
using OrderFulfillment.Application.Interfaces;
using OrderFulfillment.Application.Orders.Commands.CreateOrder;
using OrderFulfillment.Domain.Entities;
using OrderFulfillment.Domain.Exceptions;
using OrderFulfillment.Domain.ValueObjects;
using Xunit;

namespace OrderFulfillment.UnitTests.Application;

public class CreateOrderCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CreateOrderCommandHandler _handler;

    public CreateOrderCommandHandlerTests()
    {
        _orderRepository = Substitute.For<IOrderRepository>();
        _productRepository = Substitute.For<IProductRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _handler = new CreateOrderCommandHandler(_orderRepository, _productRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_WhenProductExistsAndStockIsAvailable_ShouldCreateOrderAndSaveAllChanges()
    {
        Guid userId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();
        int requestedQuantity = 2;
        Product product = new Product(productId, "PlayStation 5", new Money(500m, "USD"), 10);

        _productRepository.GetByIdAsync(productId, Arg.Any<CancellationToken>())
            .Returns(product);

        CreateOrderCommand command = new CreateOrderCommand(userId, productId, requestedQuantity);

        Guid createdOrderId = await _handler.Handle(command, CancellationToken.None);

        createdOrderId.Should().NotBeEmpty();
        product.AvailableQuantity.Should().Be(8);

        await _orderRepository.Received(1).AddAsync(
            Arg.Is<Order>(order => order.Id == createdOrderId && order.UserId == userId && order.Items.Count == 1),
            Arg.Any<CancellationToken>());

        await _productRepository.Received(1).UpdateAsync(product, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenProductDoesNotExist_ShouldThrowExceptionAndNotSaveAnything()
    {
        Guid nonExistentProductId = Guid.NewGuid();
        _productRepository.GetByIdAsync(nonExistentProductId, Arg.Any<CancellationToken>())
            .Returns((Product?)null);

        CreateOrderCommand command = new CreateOrderCommand(Guid.NewGuid(), nonExistentProductId, 1);

        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ProductNotFoundException>()
            .WithMessage($"Product '{nonExistentProductId}' not found.");

        await _orderRepository.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenStockIsInsufficient_ShouldThrowExceptionAndNotSaveOrder()
    {
        Guid productId = Guid.NewGuid();
        Product product = new Product(productId, "PlayStation 5", new Money(500m, "USD"), 1);

        _productRepository.GetByIdAsync(productId, Arg.Any<CancellationToken>())
            .Returns(product);

        CreateOrderCommand command = new CreateOrderCommand(Guid.NewGuid(), productId, 5);

        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientStockException>()
            .WithMessage("*Not enough stock*");

        product.AvailableQuantity.Should().Be(1);
        await _orderRepository.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}