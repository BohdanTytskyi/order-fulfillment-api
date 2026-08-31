using MediatR;
using OrderFulfillment.Application.Interfaces;
using OrderFulfillment.Domain.Entities;

namespace OrderFulfillment.Application.Orders.Commands.CreateOrder;

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Guid>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateOrderCommandHandler(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        Product? product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        
        if (product is null)
        {
            throw new Exception($"Product {request.ProductId} not found.");
        }

        product.Reserve(request.Quantity);

        Order order = new Order(Guid.NewGuid(), request.UserId);
        
        order.AddItem(product.Id, product.Price, request.Quantity);
        
        order.Confirm();

        await _orderRepository.AddAsync(order, cancellationToken);
        await _productRepository.UpdateAsync(product, cancellationToken);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return order.Id;
    }
}
