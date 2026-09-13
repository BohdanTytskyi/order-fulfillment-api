using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OrderFulfillment.Application.Common.Exceptions;
using OrderFulfillment.Domain.Exceptions;

namespace OrderFulfillment.Api.Infrastructure;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        (int statusCode, string title, string type, string detail) = MapException(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);
        }
        else
        {
            _logger.LogWarning("Handled application exception ({StatusCode}): {Message}", statusCode, exception.Message);
        }

        ProblemDetails problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }

    private static (int StatusCode, string Title, string Type, string Detail) MapException(Exception exception)
    {
        return exception switch
        {
            NotFoundException notFound => (
                StatusCodes.Status404NotFound,
                "Resource Not Found",
                "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                notFound.Message
            ),
            DistributedLockException lockException => (
                StatusCodes.Status409Conflict,
                "Resource Conflict",
                "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                lockException.Message
            ),
            DomainException domainException => (
                StatusCodes.Status422UnprocessableEntity,
                "Business Rule Violation",
                "https://tools.ietf.org/html/rfc4918#section-11.2",
                domainException.Message
            ),
            ArgumentException argumentException => (
                StatusCodes.Status400BadRequest,
                "Invalid Request",
                "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                argumentException.Message
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                "An unexpected error occurred. Please try again later."
            )
        };
    }
}
