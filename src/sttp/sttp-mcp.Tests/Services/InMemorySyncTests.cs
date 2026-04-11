using System.Text.Json;
using Shouldly;
using SttpMcp.Domain.Models;
using SttpMcp.Storage;

namespace SttpMcp.Tests.Services;

public sealed class InMemorySyncTests
{
    [Fact]
    public async Task Duplicate_Upsert_Should_Not_Create_Extra_Row()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryNodeStore();

        var first = await store.UpsertNodeAsync(BuildNode("sync-session"), ct);
        var second = await store.UpsertNodeAsync(BuildNode("sync-session"), ct);

        first.Status.ShouldBe(NodeUpsertStatus.Created);
        second.Status.ShouldBe(NodeUpsertStatus.Duplicate);

        var nodes = await store.QueryNodesAsync(new NodeQuery
        {
            SessionId = "sync-session",
            Limit = 10
        }, ct);

        nodes.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Change_Query_Should_Return_Incremental_Cursor()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryNodeStore();

        var first = BuildNode("sync-session") with
        {
            SyncKey = "sync-a",
            UpdatedAt = DateTime.Parse("2026-03-05T06:31:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind)
        };

        var second = BuildNode("sync-session") with
        {
            Raw = "raw-b",
            Timestamp = DateTime.Parse("2026-03-05T06:32:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind),
            SyncKey = "sync-b",
            UpdatedAt = DateTime.Parse("2026-03-05T06:33:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind)
        };

        var firstResult = await store.UpsertNodeAsync(first, ct);
        var secondResult = await store.UpsertNodeAsync(second, ct);

        var changes = await store.QueryChangesSinceAsync(
            "sync-session",
            new SyncCursor
            {
                UpdatedAt = firstResult.UpdatedAt,
                SyncKey = firstResult.SyncKey
            },
            10,
            ct);

        changes.Nodes.Count.ShouldBe(1);
        changes.Nodes[0].SyncKey.ShouldBe(secondResult.SyncKey);
    }

    [Fact]
    public async Task Put_Checkpoint_Should_Replace_Existing_Connector_State()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryNodeStore();

        await store.PutCheckpointAsync(new SyncCheckpoint
        {
            SessionId = "sync-session",
            ConnectorId = "cloud-primary",
            Cursor = new SyncCursor
            {
                UpdatedAt = DateTime.Parse("2026-03-05T06:35:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind),
                SyncKey = "sync-a"
            },
            UpdatedAt = DateTime.Parse("2026-03-05T06:36:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind),
            Metadata = JsonDocument.Parse("{\"endpoint\":\"local\"}").RootElement.Clone()
        }, ct);

        await store.PutCheckpointAsync(new SyncCheckpoint
        {
            SessionId = "sync-session",
            ConnectorId = "cloud-primary",
            Cursor = new SyncCursor
            {
                UpdatedAt = DateTime.Parse("2026-03-05T06:40:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind),
                SyncKey = "sync-b"
            },
            UpdatedAt = DateTime.Parse("2026-03-05T06:41:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind),
            Metadata = JsonDocument.Parse("{\"endpoint\":\"cloud\"}").RootElement.Clone()
        }, ct);

        var checkpoint = await store.GetCheckpointAsync("sync-session", "cloud-primary", ct);

        checkpoint.ShouldNotBeNull();
        checkpoint!.Cursor.ShouldNotBeNull();
        checkpoint.Cursor.SyncKey.ShouldBe("sync-b");
        checkpoint.Metadata.ShouldNotBeNull();
        checkpoint.Metadata!.Value.GetProperty("endpoint").GetString().ShouldBe("cloud");
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