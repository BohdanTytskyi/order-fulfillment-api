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
        ProblemDetails problemDetails = CreateProblemDetails(httpContext, exception);

        LogException(exception, problemDetails.Status ?? StatusCodes.Status500InternalServerError);

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            problemDetails.GetType(),
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }

    private static ProblemDetails CreateProblemDetails(HttpContext httpContext, Exception exception)
    {
        if (exception is ValidationException validationException)
        {
            return new HttpValidationProblemDetails(validationException.Errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Failed",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Detail = "One or more validation errors occurred.",
                Instance = httpContext.Request.Path
            };
        }

        (int statusCode, string title, string type, string detail) = MapException(exception);

        return new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
    }

    private void LogException(Exception exception, int statusCode)
    {
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);
        }
        else
        {
            _logger.LogWarning("Handled application exception ({StatusCode}): {Message}", statusCode, exception.Message);
        }
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
