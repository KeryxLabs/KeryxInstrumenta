using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Register the ACC engine TCP client (connects to localhost:9339 by default).
builder.Services.AddSingleton<AccEngineClient>();

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<AccTools>();

await builder.Build().RunAsync();
