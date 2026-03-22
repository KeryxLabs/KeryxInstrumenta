using AccCli;
using Cocona;
using Microsoft.Extensions.DependencyInjection;

var builder = CoconaApp.CreateBuilder(args);

builder.Services.AddSingleton<AccEngineClient>();

var app = builder.Build();

app.AddSubCommand("lookup", x => x.AddCommands<LookupCommands>())
    .WithDescription("Retrieve nodes and their direct relationships");

app.AddSubCommand("graph", x => x.AddCommands<GraphCommands>())
    .WithDescription("Traverse transitive dependencies in the code graph");

app.AddSubCommand("search", x => x.AddCommands<SearchCommands>())
    .WithDescription("Search nodes by name or AVEC pattern");

app.AddSubCommand("risk", x => x.AddCommands<RiskCommands>())
    .WithDescription("Find high-friction and unstable nodes");

app.AddCommand(
        "stats",
        async ([FromService] AccEngineClient client, GlobalOptions globals) =>
        {
            var result = await client.CallAsync("acc.getStats", globalOptions: globals);
            Console.WriteLine(result ?? "null");
        }
    )
    .WithDescription("Aggregate health statistics for the entire indexed codebase");

app.Run();
