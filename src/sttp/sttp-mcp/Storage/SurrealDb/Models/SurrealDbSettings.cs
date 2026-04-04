namespace SttpMcp.Storage.SurrealDb.Models;

public class SurrealDbEndpointsSettings
{
    public string? Embedded { get; set; } = "surrealkv://data/sttp-mcp";
    public string? Remote { get; set; }

    public static SurrealDbEndpointsSettings Default => new();
}

public class SurrealDbSettings
{

    public string Endpoint(bool useRemote) => useRemote && !string.IsNullOrWhiteSpace(Endpoints?.Remote)
    ? Endpoints!.Remote
    : !string.IsNullOrWhiteSpace(Endpoints?.Embedded)
        ? Endpoints!.Embedded
        : throw new Exception($"No SurrealDB endpoint configured for mode {(useRemote ? "remote" : "embedded")}. Set SurrealDb:Endpoints:{(useRemote ? "Remote" : "Embedded")} or legacy SurrealDb:Endpoint.");

    public SurrealDbEndpointsSettings? Endpoints { get; set; } = SurrealDbEndpointsSettings.Default;
    public string Namespace { get; set; } = "keryx";
    public string Database { get; set; } = "sttp-mcp";
    public string? User { get; set; } = "root";
    public string? Password { get; set; } = "root";

    public static SurrealDbSettings Default => new();
}