namespace SttpMcp.Storage.SurrealDb.Models;

public class SurrealDbEndpointsSettings
{
    public string? Embedded { get; set; }
    public string? Remote { get; set; }
}

public class SurrealDbSettings
{

    public string Endpoint (bool useRemote) => useRemote && !string.IsNullOrWhiteSpace(Endpoints?.Remote)
    ? Endpoints!.Remote   
    : !string.IsNullOrWhiteSpace(Endpoints?.Embedded) 
        ? Endpoints!.Embedded
        : throw new Exception($"No SurrealDB endpoint configured for mode {(useRemote ? "remote" : "embedded")}. Set SurrealDb:Endpoints:{(useRemote ? "Remote" : "Embedded")} or legacy SurrealDb:Endpoint.");

    public SurrealDbEndpointsSettings? Endpoints { get; set; }
    public required string Namespace { get; set; } = "keryx";
    public required string Database { get; set; } = "sttp-mcp";
    public string? User {get;set;} = "root";
    public string? Password {get;set;} = "root";
}