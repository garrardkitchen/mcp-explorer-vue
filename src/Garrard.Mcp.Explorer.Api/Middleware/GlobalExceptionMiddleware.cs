using System.Net;
using System.Text.Json;

namespace Garrard.Mcp.Explorer.Api.Middleware;

public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            var error = new { error = "An unexpected error occurred.", traceId = context.TraceIdentifier };
            await context.Response.WriteAsync(JsonSerializer.Serialize(error));
        }
    }
}
