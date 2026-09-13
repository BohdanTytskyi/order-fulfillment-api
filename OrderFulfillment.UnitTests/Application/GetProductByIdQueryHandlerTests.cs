using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderFulfillment.Application.Common.Exceptions;
using OrderFulfillment.Application.Products.Queries.GetProductById;
using OrderFulfillment.Domain.Entities;
using OrderFulfillment.Domain.ValueObjects;
using OrderFulfillment.Infrastructure.Data;
using Xunit;

namespace OrderFulfillment.UnitTests.Application;

public class GetProductByIdQueryHandlerTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_WhenProductExists_ShouldReturnProductResponseDto()
    {
        using ApplicationDbContext context = CreateInMemoryDbContext();

        Guid productId = Guid.NewGuid();
        Money price = new Money(499.99m, "USD");
        Product product = new Product(productId, "PlayStation 5 Slim", price, 15);

        await context.Products.AddAsync(product);
        await context.SaveChangesAsync();

        GetProductByIdQueryHandler handler = new GetProductByIdQueryHandler(context);
        GetProductByIdQuery query = new GetProductByIdQuery(productId);

        ProductResponseDto result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(productId);
        result.Name.Should().Be("PlayStation 5 Slim");
        result.Price.Should().Be(499.99m);
        result.Currency.Should().Be("USD");
        result.AvailableQuantity.Should().Be(15);
    }

    [Fact]
    public async Task Handle_WhenProductDoesNotExist_ShouldThrowProductNotFoundException()
    {
        using ApplicationDbContext context = CreateInMemoryDbContext();
        GetProductByIdQueryHandler handler = new GetProductByIdQueryHandler(context);

        Guid nonExistentProductId = Guid.NewGuid();
        GetProductByIdQuery query = new GetProductByIdQuery(nonExistentProductId);

        Func<Task> act = async () => await handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<ProductNotFoundException>()
            .WithMessage($"Product '{nonExistentProductId}' not found.");
    }
}
