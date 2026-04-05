namespace sttp_ui.Models;

public sealed record HealthResponse(string Status, string Transport);

public sealed record StoreContextRequest(string Node, string SessionId);

public sealed record StoreContextResponse(string NodeId, float Psi, bool Valid, string? ValidationError);

public sealed record CalibrateSessionRequest(
    string SessionId,
    float Stability,
    float Friction,
    float Logic,
    float Autonomy,
    string Trigger);

public sealed record AvecState(float Stability, float Friction, float Logic, float Autonomy, float Psi);

public sealed record CalibrateSessionResponse(
    AvecState PreviousAvec,
    float Delta,
    string DriftClassification,
    string Trigger,
    IReadOnlyList<string> TriggerHistory,
    bool IsFirstCalibration);

public sealed record ListNodesResponse(IReadOnlyList<SttpNodeDto> Nodes, int Retrieved);

public sealed record SttpNodeDto(
    string Raw,
    string SessionId,
    string Tier,
    DateTime Timestamp,
    int CompressionDepth,
    string? ParentNodeId,
    AvecState UserAvec,
    AvecState ModelAvec,
    AvecState? CompressionAvec,
    float Rho,
    float Kappa,
    float Psi);
