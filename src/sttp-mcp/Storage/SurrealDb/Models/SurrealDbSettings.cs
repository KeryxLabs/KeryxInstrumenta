namespace SttpMcp.Storage.SurrealDb.Models;

public class SurrealDbSettings
{
    public required string Endpoint {get;set;}
    public required string Namespace {get;set;}
    public required string Database {get;set;}
}