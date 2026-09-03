using Microsoft.AspNetCore.Mvc;
using OrderFulfillment.Application.Interfaces;
using OrderFulfillment.Domain.Entities;
using OrderFulfillment.Domain.ValueObjects;

namespace OrderFulfillment.Api.Controllers;

public record CreateProductRequest(string Name, decimal PriceAmount, string Currency, int InitialQuantity);

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProductsController(IProductRepository productRepository, IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Product? product = await _productRepository.GetByIdAsync(id, cancellationToken);
        
        if (product is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            product.Id,
            product.Name,
            Price = product.Price.Amount,
            Currency = product.Price.Currency,
            product.AvailableQuantity
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        Money price = new Money(request.PriceAmount, request.Currency);
        Product product = new Product(Guid.NewGuid(), request.Name, price, request.InitialQuantity);

        await _productRepository.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new { ProductId = product.Id });
    }
}