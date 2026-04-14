using SttpMcp.Domain.Contracts;
using SttpMcp.Domain.Models;

namespace SttpMcp.Application.Services;

public sealed class RekeyScopeService(INodeStore store)
{
    public Task<BatchRekeyResult> RekeyAsync(
        IReadOnlyList<string> nodeIds,
        string targetTenantId,
        string targetSessionId,
        bool dryRun,
        bool allowMerge,
        CancellationToken ct = default)
        => store.BatchRekeyScopesAsync(nodeIds, targetTenantId, targetSessionId, dryRun, allowMerge, ct);
}