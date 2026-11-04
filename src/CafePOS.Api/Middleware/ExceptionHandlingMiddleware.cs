using System.Text.Json;
using CafePOS.Core.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CafePOS.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted) throw;

            var (status, message) = ex switch
            {
                UnauthorizedAccessException => (StatusCodes.Status403Forbidden, ex.Message),
                KeyNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
                InvalidOperationException => (StatusCodes.Status400BadRequest, ex.Message),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
                    "This record was changed by another user. Please reload and try again."),
                DbUpdateException => (StatusCodes.Status400BadRequest,
                    "The change conflicts with existing data. Please check for duplicate values."),
                ArgumentException => (StatusCodes.Status400BadRequest, ex.Message),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again.")
            };

            if (ex is DbUpdateConcurrencyException concurrency)
                _logger.LogWarning("Concurrency conflict entities: {Entities}",
                    string.Join("; ", concurrency.Entries.Select(e =>
                        $"{e.Metadata.ClrType.Name}/{e.State}/{string.Join(",", e.Properties.Where(p => p.Metadata.IsConcurrencyToken).Select(p => p.Metadata.Name))}")));

            if (status >= 500) _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            else _logger.LogWarning(ex, "Request failed with {Status} for {Method} {Path}", status, context.Request.Method, context.Request.Path);

            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            var payload = new ApiErrorResponse(message,
                _env.IsDevelopment() ? ex.Message : null);
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }
    }
}
