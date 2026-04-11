namespace SttpGateway.Contracts;

public sealed record CalibrateSessionHttpRequest(
    string SessionId,
    string? TenantId,
    float Stability,
    float Friction,
    float Logic,
    float Autonomy,
    string Trigger);

public sealed record StoreContextHttpRequest(
    string Node,
    string SessionId,
    string? TenantId);

public sealed record GetContextHttpRequest(
    string SessionId,
    string? TenantId,
    float Stability,
    float Friction,
    float Logic,
    float Autonomy,
    int Limit = 5);

public sealed record CreateMonthlyRollupHttpRequest(
    string SessionId,
    string? TenantId,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    string? SourceSessionId = null,
    string? ParentNodeId = null,
    bool Persist = true,
    int Limit = 5000);

public sealed record BatchRekeyHttpRequest(
    IReadOnlyList<string> NodeIds,
    string TargetSessionId,
    string? TargetTenantId = null,
    bool DryRun = true,
    bool AllowMerge = false);
