using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OrderFulfillment.Api.Infrastructure;
using OrderFulfillment.Application.Common.Exceptions;
using OrderFulfillment.Domain.Exceptions;
using Xunit;

namespace OrderFulfillment.UnitTests.Api;

public class GlobalExceptionHandlerTests
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly GlobalExceptionHandler _handler;
    private readonly JsonSerializerOptions _jsonOptions;

    public GlobalExceptionHandlerTests()
    {
        _logger = Substitute.For<ILogger<GlobalExceptionHandler>>();
        _handler = new GlobalExceptionHandler(_logger);
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    private static DefaultHttpContext CreateHttpContext(string path = "/api/test")
    {
        DefaultHttpContext context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = path;
        return context;
    }

    private async Task<ProblemDetails?> ReadProblemDetailsAsync(HttpResponse response)
    {
        response.Body.Seek(0, SeekOrigin.Begin);
        using StreamReader reader = new StreamReader(response.Body);
        string json = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<ProblemDetails>(json, _jsonOptions);
    }

    [Fact]
    public async Task TryHandleAsync_WhenNotFoundException_Returns404NotFoundProblemDetails()
    {
        DefaultHttpContext context = CreateHttpContext("/api/orders");
        Guid productId = Guid.NewGuid();
        ProductNotFoundException exception = new ProductNotFoundException(productId);

        bool handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        context.Response.ContentType.Should().StartWith("application/problem+json");

        ProblemDetails? problem = await ReadProblemDetailsAsync(context.Response);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(404);
        problem.Title.Should().Be("Resource Not Found");
        problem.Detail.Should().Contain(productId.ToString());
        problem.Instance.Should().Be("/api/orders");
    }

    [Fact]
    public async Task TryHandleAsync_WhenDistributedLockException_Returns409ConflictProblemDetails()
    {
        DefaultHttpContext context = CreateHttpContext("/api/orders");
        string lockKey = "product:123";
        DistributedLockException exception = new DistributedLockException(lockKey);

        bool handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        context.Response.ContentType.Should().StartWith("application/problem+json");

        ProblemDetails? problem = await ReadProblemDetailsAsync(context.Response);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(409);
        problem.Title.Should().Be("Resource Conflict");
        problem.Detail.Should().Contain(lockKey);
        problem.Instance.Should().Be("/api/orders");
    }

    [Fact]
    public async Task TryHandleAsync_WhenDomainException_Returns422UnprocessableEntityProblemDetails()
    {
        DefaultHttpContext context = CreateHttpContext("/api/orders");
        InsufficientStockException exception = new InsufficientStockException("PlayStation 5 Pro", 0, 1);

        bool handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        context.Response.ContentType.Should().StartWith("application/problem+json");

        ProblemDetails? problem = await ReadProblemDetailsAsync(context.Response);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(422);
        problem.Title.Should().Be("Business Rule Violation");
        problem.Detail.Should().Contain("Not enough stock for product 'PlayStation 5 Pro'");
        problem.Instance.Should().Be("/api/orders");
    }

    [Fact]
    public async Task TryHandleAsync_WhenArgumentException_Returns400BadRequestProblemDetails()
    {
        DefaultHttpContext context = CreateHttpContext("/api/orders");
        ArgumentException exception = new ArgumentException("Quantity must be greater than zero.");

        bool handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().StartWith("application/problem+json");

        ProblemDetails? problem = await ReadProblemDetailsAsync(context.Response);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(400);
        problem.Title.Should().Be("Invalid Request");
        problem.Detail.Should().Be("Quantity must be greater than zero.");
        problem.Instance.Should().Be("/api/orders");
    }

    [Fact]
    public async Task TryHandleAsync_WhenUnhandledException_Returns500InternalServerErrorWithSanitizedMessage()
    {
        DefaultHttpContext context = CreateHttpContext("/api/orders");
        Exception exception = new InvalidOperationException("Fatal database connection string leak: password=secret");

        bool handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().StartWith("application/problem+json");

        ProblemDetails? problem = await ReadProblemDetailsAsync(context.Response);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(500);
        problem.Title.Should().Be("Internal Server Error");
        problem.Detail.Should().Be("An unexpected error occurred. Please try again later.");
        problem.Detail.Should().NotContain("password=secret");
        problem.Instance.Should().Be("/api/orders");
    }

    [Fact]
    public async Task TryHandleAsync_WhenValidationException_Returns400BadRequestWithErrorsDictionary()
    {
        DefaultHttpContext context = CreateHttpContext("/api/orders");
        Dictionary<string, string[]> validationErrors = new Dictionary<string, string[]>
        {
            { "Quantity", new string[] { "Quantity must be greater than zero." } },
            { "CustomerId", new string[] { "CustomerId is required." } }
        };
        ValidationException exception = new ValidationException(validationErrors);

        bool handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().StartWith("application/problem+json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using StreamReader reader = new StreamReader(context.Response.Body);
        string json = await reader.ReadToEndAsync();
        HttpValidationProblemDetails? problem = JsonSerializer.Deserialize<HttpValidationProblemDetails>(json, _jsonOptions);

        problem.Should().NotBeNull();
        problem!.Status.Should().Be(400);
        problem.Title.Should().Be("Validation Failed");
        problem.Errors.Should().ContainKey("Quantity");
        problem.Errors["Quantity"].Should().Contain("Quantity must be greater than zero.");
        problem.Errors.Should().ContainKey("CustomerId");
        problem.Errors["CustomerId"].Should().Contain("CustomerId is required.");
        problem.Instance.Should().Be("/api/orders");
    }

    [Fact]
    public async Task TryHandleAsync_WhenOrderNotFoundException_Returns404NotFoundProblemDetails()
    {
        DefaultHttpContext context = CreateHttpContext("/api/orders/details");
        Guid orderId = Guid.NewGuid();
        OrderNotFoundException exception = new OrderNotFoundException(orderId);

        bool handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        context.Response.ContentType.Should().StartWith("application/problem+json");

        ProblemDetails? problem = await ReadProblemDetailsAsync(context.Response);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(404);
        problem.Title.Should().Be("Resource Not Found");
        problem.Detail.Should().Contain(orderId.ToString());
        problem.Instance.Should().Be("/api/orders/details");
    }
}
