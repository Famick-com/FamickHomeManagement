using System.Net;
using System.Text.Json;
using Famick.HomeManagement.Core.Exceptions;
using FluentValidation;

namespace Famick.HomeManagement.Web.Middleware;

/// <summary>
/// Middleware for global exception handling and mapping to HTTP status codes
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorResponse) = MapExceptionToResponse(exception);

        // Log based on severity
        if (statusCode >= 500)
        {
            _logger.LogError(exception, "Internal server error: {Message}", exception.Message);
        }
        else if (statusCode >= 400)
        {
            _logger.LogWarning(exception, "Client error {StatusCode}: {Message}", statusCode, exception.Message);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, options));
    }

    private (int statusCode, object errorResponse) MapExceptionToResponse(Exception exception)
    {
        return exception switch
        {
            // 400 Bad Request - Validation errors
            ValidationException validationEx => (
                (int)HttpStatusCode.BadRequest,
                new
                {
                    error_message = "Validation failed",
                    errors = validationEx.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.ErrorMessage).ToArray()
                        )
                }
            ),

            // 401 Unauthorized - Invalid credentials (most specific first)
            InvalidCredentialsException => (
                (int)HttpStatusCode.Unauthorized,
                new { error_message = exception.Message }
            ),

            // 403 Forbidden - Account inactive (more specific than AuthenticationException)
            AccountInactiveException => (
                (int)HttpStatusCode.Forbidden,
                new { error_message = exception.Message }
            ),

            // 401 Unauthorized - General authentication failures
            AuthenticationException => (
                (int)HttpStatusCode.Unauthorized,
                new { error_message = exception.Message }
            ),

            // 404 Not Found - Entity not found
            EntityNotFoundException => (
                (int)HttpStatusCode.NotFound,
                new { error_message = exception.Message }
            ),

            // 409 Conflict - Duplicate entity
            DuplicateEntityException => (
                (int)HttpStatusCode.Conflict,
                new { error_message = exception.Message }
            ),

            // 422 Unprocessable Entity - Business rule violations
            // (includes CircularDependencyException, InsufficientStockException)
            BusinessRuleViolationException => (
                (int)HttpStatusCode.UnprocessableEntity,
                new { error_message = exception.Message }
            ),

            // 400 Bad Request - Generic domain exception
            DomainException domainEx => (
                (int)HttpStatusCode.BadRequest,
                new { error_message = domainEx.Message }
            ),

            // 500 Internal Server Error - Unhandled exceptions
            _ => (
                (int)HttpStatusCode.InternalServerError,
                new
                {
                    error_message = _environment.IsDevelopment()
                        ? exception.Message
                        : "An internal server error occurred. Please try again later."
                }
            )
        };
    }
}

/// <summary>
/// Extension method for registering the exception handling middleware
/// </summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
