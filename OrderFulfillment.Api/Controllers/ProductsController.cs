using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderFulfillment.Application.Interfaces;
using OrderFulfillment.Application.Products.Queries.GetProductById;
using OrderFulfillment.Domain.Entities;
using OrderFulfillment.Domain.ValueObjects;

namespace OrderFulfillment.Api.Controllers;

public record CreateProductRequest(string Name, decimal PriceAmount, string Currency, int InitialQuantity);

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProductsController(
        ISender sender,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork)
    {
        _sender = sender;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        ProductResponseDto product = await _sender.Send(new GetProductByIdQuery(id), cancellationToken);
        return Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        Money price = new Money(request.PriceAmount, request.Currency);
        Product product = new Product(Guid.NewGuid(), request.Name, price, request.InitialQuantity);

        await _productRepository.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, new { ProductId = product.Id });
    }
}