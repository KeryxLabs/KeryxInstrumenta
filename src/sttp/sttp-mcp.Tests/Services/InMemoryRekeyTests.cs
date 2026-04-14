using Shouldly;
using SttpMcp.Domain.Models;
using SttpMcp.Storage;

namespace SttpMcp.Tests.Services;

public sealed class InMemoryRekeyTests
{
    [Fact]
    public async Task Batch_Rekey_Should_Move_Scoped_Nodes_And_Calibrations()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryNodeStore();
        var sourceSessionId = "tenant:acme::session:source-session";
        var targetSessionId = "tenant:acme::session:target-session";

        var upsert = await store.UpsertNodeAsync(BuildNode(sourceSessionId) with { SyncKey = "sync-a" }, ct);
        await store.StoreCalibrationAsync(sourceSessionId, new AvecState
        {
            Stability = 0.8f,
            Friction = 0.2f,
            Logic = 0.9f,
            Autonomy = 0.7f
        }, "manual", ct);

        var result = await store.BatchRekeyScopesAsync(
            [upsert.NodeId],
            "acme",
            targetSessionId,
            dryRun: false,
            allowMerge: false,
            ct);

        result.ResolvedNodeIds.ShouldBe(1);
        result.TemporalNodesUpdated.ShouldBe(1);
        result.CalibrationsUpdated.ShouldBe(1);
        result.Scopes.Count.ShouldBe(1);
        result.Scopes[0].Applied.ShouldBeTrue();

        var movedNodes = await store.QueryNodesAsync(new NodeQuery
        {
            SessionId = targetSessionId,
            Limit = 10
        }, ct);

        movedNodes.Count.ShouldBe(1);
        movedNodes[0].SessionId.ShouldBe(targetSessionId);

        var history = await store.GetTriggerHistoryAsync(targetSessionId, ct);
        history.Count.ShouldBe(1);
        history[0].ShouldBe("manual");
    }

    private static SttpNode BuildNode(string sessionId) => new()
    {
        Raw = "raw",
        SessionId = sessionId,
        Tier = "raw",
        Timestamp = DateTime.Parse("2026-03-05T06:30:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind),
        CompressionDepth = 1,
        ParentNodeId = null,
        SyncKey = string.Empty,
        UpdatedAt = DateTime.Parse("2026-03-05T06:30:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind),
        SourceMetadata = null,
        UserAvec = new AvecState
        {
            Stability = 0.85f,
            Friction = 0.25f,
            Logic = 0.80f,
            Autonomy = 0.70f
        },
        ModelAvec = new AvecState
        {
            Stability = 0.91f,
            Friction = 0.21f,
            Logic = 0.90f,
            Autonomy = 0.80f
        },
        CompressionAvec = AvecState.Zero,
        Rho = 0.96f,
        Kappa = 0.94f,
        Psi = 2.6f
    };
}