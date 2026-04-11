using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SttpMcp.Domain.Models;

public record SttpNode
{
    public required string Raw { get; init; }
    public required string SessionId { get; init; }
    public required string Tier { get; init; }
    public required DateTime Timestamp { get; init; }
    public required int CompressionDepth { get; init; }
    public string? ParentNodeId { get; init; }
    public required string SyncKey { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public JsonElement? SourceMetadata { get; init; }
    public required AvecState UserAvec { get; init; }
    public required AvecState ModelAvec { get; init; }
    public AvecState? CompressionAvec { get; init; }
    public required float Rho { get; init; }
    public required float Kappa { get; init; }
    public required float Psi { get; init; }

    public string CanonicalSyncKey()
    {
        var fingerprint = new SyncFingerprint(
            SessionId,
            Tier,
            Timestamp,
            CompressionDepth,
            ParentNodeId,
            Raw,
            UserAvec,
            ModelAvec,
            CompressionAvec,
            Rho,
            Kappa,
            Psi);

        var json = JsonSerializer.Serialize(fingerprint);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record SyncFingerprint(
        string SessionId,
        string Tier,
        DateTime Timestamp,
        int CompressionDepth,
        string? ParentNodeId,
        string Raw,
        AvecState UserAvec,
        AvecState ModelAvec,
        AvecState? CompressionAvec,
        float Rho,
        float Kappa,
        float Psi);
}