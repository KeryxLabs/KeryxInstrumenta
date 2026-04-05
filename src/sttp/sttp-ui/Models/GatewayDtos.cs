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

public sealed record GraphResponse(
    IReadOnlyList<GraphSessionDto> Sessions,
    IReadOnlyList<GraphNodeDto> Nodes,
    IReadOnlyList<GraphEdgeDto> Edges,
    int Retrieved);

public sealed record GraphSessionDto(
    string Id,
    string Label,
    int NodeCount,
    float AvgPsi,
    DateTime LastModified,
    int Size);

public sealed record GraphNodeDto(
    string Id,
    string SessionId,
    string Label,
    string Tier,
    DateTime Timestamp,
    float Psi,
    string? ParentNodeId,
    int Size);

public sealed record GraphEdgeDto(
    string Id,
    string Source,
    string Target,
    string Kind);

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

public sealed record AiSummary(
    string Topic,
    string WhatHappened,
    string WhereWeLeftOff,
    string Vibe,
    string PickBackUpWith);
