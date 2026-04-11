using SttpMcp.Domain.Models;

namespace SttpMcp.Domain.Contracts;

public interface ISyncCoordinatorPolicy
{
    bool ShouldAcceptNode(SttpNode node) => true;

    ConnectorMetadata? BuildCheckpointMetadata(
        string sessionId,
        string connectorId,
        SyncCheckpoint? previous,
        SttpNode? lastAppliedNode,
        SyncCursor? nextCursor)
        => previous?.Metadata;
}