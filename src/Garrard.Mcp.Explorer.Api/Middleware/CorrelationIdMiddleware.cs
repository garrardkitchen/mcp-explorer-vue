namespace Garrard.Mcp.Explorer.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId))
            correlationId = Guid.NewGuid().ToString();

        context.Response.Headers[CorrelationIdHeader] = correlationId.ToString();
        context.TraceIdentifier = correlationId.ToString();
        await next(context);
    }
}
