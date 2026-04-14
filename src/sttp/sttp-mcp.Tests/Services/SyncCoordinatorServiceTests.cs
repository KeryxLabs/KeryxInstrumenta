using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using SttpMcp.Application.Services;
using SttpMcp.Domain.Contracts;
using SttpMcp.Domain.Models;
using SttpMcp.Storage;

namespace SttpMcp.Tests.Services;

public sealed class SyncCoordinatorServiceTests
{
    [Fact]
    public async Task Pull_Should_Not_Resurface_RemoteRows_As_LocalChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryNodeStore();
        var cursorUpdatedAt = DateTime.Parse(
            "2026-03-05T06:41:00Z",
            null,
            System.Globalization.DateTimeStyles.RoundtripKind);

        var source = new StubChangeSource(new ChangeQueryResult
        {
            Nodes =
            [
                BuildNode("sync-session", "remote", "sync-a", "2026-03-05T06:41:00Z")
            ],
            NextCursor = new SyncCursor
            {
                UpdatedAt = cursorUpdatedAt,
                SyncKey = "sync-a"
            },
            HasMore = false
        });

        var coordinator = new SyncCoordinatorService(
            store,
            source,
            NullLogger<SyncCoordinatorService>.Instance);

        var result = await coordinator.PullAsync(new SyncPullRequest
        {
            SessionId = "sync-session",
            ConnectorId = "cloud-primary",
            PageSize = 50,
            MaxBatches = 1
        }, ct);

        result.Checkpoint.ShouldNotBeNull();
        result.Checkpoint!.Cursor.ShouldNotBeNull();

        var changes = await store.QueryChangesSinceAsync(
            "sync-session",
            result.Checkpoint.Cursor,
            50,
            ct);

        changes.Nodes.Count.ShouldBe(0);
        changes.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task Pull_Should_Page_Changes_And_Advance_Checkpoint_Without_Owning_Policy()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryNodeStore();
        var source = new StubChangeSource(new ChangeQueryResult
        {
            Nodes =
            [
                BuildNode("sync-session", "apply", "sync-a", "2026-03-05T06:31:00Z"),
                BuildNode("sync-session", "skip", "sync-b", "2026-03-05T06:32:00Z")
            ],
            NextCursor = new SyncCursor
            {
                UpdatedAt = DateTime.Parse("2026-03-05T06:32:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind),
                SyncKey = "sync-b"
            },
            HasMore = false
        });

        var coordinator = new SyncCoordinatorService(
            store,
            source,
            NullLogger<SyncCoordinatorService>.Instance,
            new RejectSkipPolicy());

        var result = await coordinator.PullAsync(new SyncPullRequest
        {
            SessionId = "sync-session",
            ConnectorId = "cloud-primary",
            PageSize = 50,
            MaxBatches = 1
        }, ct);

        result.Fetched.ShouldBe(2);
        result.Created.ShouldBe(1);
        result.Filtered.ShouldBe(1);
        result.HasMore.ShouldBeFalse();
        result.Checkpoint.ShouldNotBeNull();
        result.Checkpoint!.Cursor.ShouldNotBeNull();
        result.Checkpoint.Cursor.SyncKey.ShouldBe("sync-b");

        var nodes = await store.QueryNodesAsync(new NodeQuery
        {
            SessionId = "sync-session",
            Limit = 10
        }, ct);

        nodes.Count.ShouldBe(1);
        nodes[0].Raw.ShouldBe("apply");
    }

    private sealed class StubChangeSource(ChangeQueryResult page) : ISyncChangeSource
    {
        private ChangeQueryResult? _page = page;

        public Task<ChangeQueryResult> ReadChangesAsync(
            string sessionId,
            string connectorId,
            SyncCursor? cursor,
            int limit,
            CancellationToken ct = default)
        {
            var page = _page ?? new ChangeQueryResult();
            _page = null;
            return Task.FromResult(page);
        }
    }

    private sealed class RejectSkipPolicy : ISyncCoordinatorPolicy
    {
        public bool ShouldAcceptNode(SttpNode node) => node.Raw != "skip";
    }

    private static SttpNode BuildNode(string sessionId, string raw, string syncKey, string updatedAt) => new()
    {
        Raw = raw,
        SessionId = sessionId,
        Tier = "raw",
        Timestamp = DateTime.Parse("2026-03-05T06:30:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind),
        CompressionDepth = 1,
        ParentNodeId = null,
        SyncKey = syncKey,
        UpdatedAt = DateTime.Parse(updatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
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