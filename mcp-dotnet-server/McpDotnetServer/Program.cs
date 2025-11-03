using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

// ============================================================================
// APPLICATION STARTUP
// ============================================================================
var builder = WebApplication.CreateBuilder(args);

// Add MCP services with HTTP transport
builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new Implementation
    {
        Name = "risk-scorer-mcp",
        Version = "1.0.0"
    };
})
.WithHttpTransport()
.WithToolsFromAssembly();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Use CORS
app.UseCors();

// Health check endpoint
app.MapGet("/health", () => Results.Json(new
{
    status = "ok",
    name = "risk-scorer-mcp",
    version = "1.0.0"
}));

// Map MCP endpoint
app.MapMcp("/mcp");

// Startup message
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine("=".PadRight(60, '='));
    Console.WriteLine("🚀 Risk Scorer MCP Server (.NET)");
    Console.WriteLine("=".PadRight(60, '='));
    Console.WriteLine($"📡 HTTP: http://localhost:{port}/mcp");
    Console.WriteLine($"💚 Health: http://localhost:{port}/health");
    Console.WriteLine($"🧩 Tools: 3 available");
    Console.WriteLine("=".PadRight(60, '='));
});

app.Run($"http://0.0.0.0:{port}");
