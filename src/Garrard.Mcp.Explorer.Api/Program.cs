using Asp.Versioning;
using Garrard.Mcp.Explorer.Api.Middleware;
using Garrard.Mcp.Explorer.Infrastructure.DependencyInjection;
using Garrard.Mcp.Explorer.Infrastructure.Mcp;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpExplorerInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // Serialize enums as their C# names (PascalCase) — e.g. "CustomHeaders", "AzureClientCredentials".
        // allowIntegerValues:true means existing int-encoded enum values from v1 still deserialise.
        opts.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(allowIntegerValues: true));
    });
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("WebhookCapture", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromSeconds(10);
        limiterOptions.PermitLimit = 60;
        limiterOptions.QueueLimit = 0;
    });
});

builder.Services.AddSingleton<ConnectionUpdateService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ConnectionUpdateService>());

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("DevCors");
}

app.UseRateLimiter();

app.MapGet("/oauth/callback", (HttpContext httpContext, OAuthCallbackService oauthCallback) =>
{
    var fullUri = new Uri($"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.Path}{httpContext.Request.QueryString}");
    oauthCallback.CompleteCallback(fullUri);
    return Results.Content(
        "<html><head><title>MCP Explorer — OAuth</title></head>" +
        "<body style=\"font-family:sans-serif;padding:2rem\">" +
        "<h2>✅ Authentication complete</h2>" +
        "<p>You may close this tab and return to MCP Explorer.</p></body></html>",
        "text/html");
});

app.MapControllers();
app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }));
await app.RunAsync();

public partial class Program { }
