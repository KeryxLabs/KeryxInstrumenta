using System.Text.Json.Serialization;
using SttpMcp.Domain.Models;

namespace SttpMcp.Storage.SurrealDb.Models;

public sealed record SurrealNodeRecord(
    [property: JsonPropertyName("SessionId")] string SessionId,
    [property: JsonPropertyName("Raw")] string Raw,
    [property: JsonPropertyName("Tier")] string Tier,
    [property: JsonPropertyName("Timestamp")] DateTime Timestamp,
    [property: JsonPropertyName("CompressionDepth")] int CompressionDepth,
    [property: JsonPropertyName("ParentNodeId")] string? ParentNodeId,
    [property: JsonPropertyName("SyncKey")] string? SyncKey,
    [property: JsonPropertyName("UpdatedAt")] DateTime? UpdatedAt,
    [property: JsonPropertyName("SourceMetadata")] ConnectorMetadata? SourceMetadata,
    [property: JsonPropertyName("Psi")] double Psi,
    [property: JsonPropertyName("Rho")] double Rho,
    [property: JsonPropertyName("Kappa")] double Kappa,
    [property: JsonPropertyName("UserStability")] double UserStability,
    [property: JsonPropertyName("UserFriction")] double UserFriction,
    [property: JsonPropertyName("UserLogic")] double UserLogic,
    [property: JsonPropertyName("UserAutonomy")] double UserAutonomy,
    [property: JsonPropertyName("UserPsi")] double UserPsi,
    [property: JsonPropertyName("ModelStability")] double ModelStability,
    [property: JsonPropertyName("ModelFriction")] double ModelFriction,
    [property: JsonPropertyName("ModelLogic")] double ModelLogic,
    [property: JsonPropertyName("ModelAutonomy")] double ModelAutonomy,
    [property: JsonPropertyName("ModelPsi")] double ModelPsi,
    [property: JsonPropertyName("CompStability")] double CompStability,
    [property: JsonPropertyName("CompFriction")] double CompFriction,
    [property: JsonPropertyName("CompLogic")] double CompLogic,
    [property: JsonPropertyName("CompAutonomy")] double CompAutonomy,
    [property: JsonPropertyName("CompPsi")] double CompPsi,
    [property: JsonPropertyName("ResonanceDelta")] double ResonanceDelta);