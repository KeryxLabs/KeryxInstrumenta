namespace SttpGateway.Contracts;

public sealed record CalibrateSessionHttpRequest(
    string SessionId,
    float Stability,
    float Friction,
    float Logic,
    float Autonomy,
    string Trigger);

public sealed record StoreContextHttpRequest(
    string Node,
    string SessionId);

public sealed record GetContextHttpRequest(
    string SessionId,
    float Stability,
    float Friction,
    float Logic,
    float Autonomy,
    int Limit = 5);

public sealed record CreateMonthlyRollupHttpRequest(
    string SessionId,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    string? SourceSessionId = null,
    string? ParentNodeId = null,
    bool Persist = true,
    int Limit = 5000);
