using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SttpMcp.Application.Tools;
using SttpMcp.Domain.Contracts;
using SttpMcp.Storage;
using SttpMcp.Storage.SurrealDb;
using SttpMcp.Storage.SurrealDb.Models;
using SurrealDb.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("./appsettings.json", optional: false, reloadOnChange: true);


// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);


var surrealSettings = builder.Configuration.GetSection("SurrealDB").Get<SurrealDbSettings>() ?? throw new Exception("SurrealDb settings not passed");

Directory.CreateDirectory("data");

var options = SurrealDbOptions.Create()
    .WithEndpoint(surrealSettings.Endpoint)
    .WithNamespace(surrealSettings.Namespace)
    .WithDatabase(surrealSettings.Database)
    .Build();

builder.Services
    .AddSurreal(options)
    .AddSurrealKvProvider();

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services   
    .AddSingleton<INodeStore, SurrealDbNodeStore>()
    .AddSingleton<INodeValidator, TreeSitterValidator>()
    .AddSingleton<CalibrateSessionTool>()
    .AddSingleton<StoreContextTool>()
    .AddSingleton<GetContextTool>()
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<CalibrateSessionTool>()
    .WithTools<StoreContextTool>()
    .WithTools<GetContextTool>();


var app = builder.Build();

// bootstrap schema on startup
var store = app.Services.GetRequiredService<INodeStore>();
if (store is SurrealDbNodeStore surrealStore)
    await surrealStore.InitializeAsync();

await app.RunAsync();
