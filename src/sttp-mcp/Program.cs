using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SttpMcp.Application.Tools;
using SttpMcp.Domain.Contracts;
using SttpMcp.Storage;
using SurrealDb.Net;

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services
    .AddSingleton<INodeStore, InMemoryNodeStore>()
    .AddSingleton<INodeValidator, TreeSitterValidator>()
    .AddSingleton<CalibrateSessionTool>()
    .AddSingleton<StoreContextTool>()
    .AddSingleton<GetContextTool>()
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<CalibrateSessionTool>()
    .WithTools<StoreContextTool>()
    .WithTools<GetContextTool>();


    
// builder.Services.AddSurreal("surrealkv://temporal_data.db")
//                 .AddSurrealKvProvider(); 

await builder.Build().RunAsync();
