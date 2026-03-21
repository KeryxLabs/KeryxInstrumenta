using AdaptiveCodecContextEngine.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides query operations for the ACC dimensional code graph.
/// </summary>
public interface IAccQueryService
{
    /// <summary>
    /// Retrieves a single node by its unique identifier, including full AVEC scores and relationships.
    /// </summary>
    /// <param name="nodeId">The unique node identifier (e.g., "UserService.cs:AuthenticateAsync:23")</param>
    /// <returns>The node with all metrics and scores, or null if not found</returns>
    Task<NodeDto?> GetNodeAsync(string nodeId);

    /// <summary>
    /// Queries a node and its immediate relationships (incoming and outgoing edges).
    /// </summary>
    /// <param name="nodeId">The unique node identifier</param>
    /// <param name="includeScores">Whether to include AVEC dimensional scores in the response</param>
    /// <returns>The node with its direct relationships, or null if not found</returns>
    Task<NodeDto?> QueryRelationsAsync(string nodeId, bool includeScores = false);

    /// <summary>
    /// Performs graph traversal to find all dependencies of a node (transitive closure).
    /// Useful for impact analysis - "what breaks if I change this?"
    /// </summary>
    /// <param name="nodeId">The unique node identifier</param>
    /// <param name="direction">Direction of traversal: Incoming (what depends on this), Outgoing (what this depends on), or Both</param>
    /// <param name="maxDepth">Maximum traversal depth. Use -1 for unlimited depth</param>
    /// <param name="includeScores">Whether to include AVEC dimensional scores</param>
    /// <returns>List of all nodes in the dependency graph</returns>
    Task<List<NodeDto>> QueryDependenciesAsync(
        string nodeId,
        DependencyDirection direction = DependencyDirection.Both,
        int maxDepth = -1,
        bool includeScores = false
    );

    /// <summary>
    /// Finds nodes with similar dimensional profiles using Euclidean distance in 4D AVEC space.
    /// Useful for finding architectural patterns - "show me code similar to this."
    /// </summary>
    /// <param name="targetProfile">The AVEC profile to match against (stability, logic, friction, autonomy)</param>
    /// <param name="threshold">Similarity threshold from 0.0 (very different) to 1.0 (identical). Default 0.8</param>
    /// <returns>List of nodes clustered near the target profile in dimensional space</returns>
    Task<List<NodeDto>> QueryPatternsAsync(AvecScores targetProfile, double threshold = 0.8);

    /// <summary>
    /// Searches for nodes by name using substring matching.
    /// </summary>
    /// <param name="name">The search term (case-insensitive substring match)</param>
    /// <param name="limit">Maximum number of results to return. Default 10</param>
    /// <returns>List of matching nodes</returns>
    Task<List<NodeDto>> SearchByNameAsync(string name, int limit = 10);

    /// <summary>
    /// Finds nodes with high friction (many incoming dependencies, central chokepoints).
    /// These are risky to change as they affect many other parts of the codebase.
    /// </summary>
    /// <param name="minFriction">Minimum friction score (0.0-1.0). Default 0.7</param>
    /// <param name="limit">Maximum number of results. Default 20</param>
    /// <returns>List of high-friction nodes ordered by friction (highest first)</returns>
    Task<List<NodeDto>> GetNodesWithHighFrictionAsync(double minFriction = 0.7, int limit = 20);

    /// <summary>
    /// Finds nodes with low stability (high churn, many contributors, low test coverage).
    /// These are more likely to break or introduce bugs.
    /// </summary>
    /// <param name="maxStability">Maximum stability score (0.0-1.0). Default 0.4</param>
    /// <param name="limit">Maximum number of results. Default 20</param>
    /// <returns>List of unstable nodes ordered by stability (lowest first)</returns>
    Task<List<NodeDto>> GetUnstableNodesAsync(double maxStability = 0.4, int limit = 20);

    /// <summary>
    /// Retrieves aggregate statistics for the entire project.
    /// Includes total node count and average AVEC scores across all nodes.
    /// </summary>
    /// <returns>Project-wide statistics</returns>
    Task<ProjectStatsDto> GetProjectStatsAsync();
}

public class AccQueryService : IAccQueryService
{
    private readonly SurrealDbRepository _repository;
    private readonly ILogger<AccQueryService> _logger;

    public AccQueryService(SurrealDbRepository repository, ILogger<AccQueryService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<NodeDto?> GetNodeAsync(string nodeId)
    {
        return await _repository.QueryRelationsAsync(nodeId, includeScores: true);
    }

    public async Task<NodeDto?> QueryRelationsAsync(string nodeId, bool includeScores = false)
    {
        return await _repository.QueryRelationsAsync(nodeId, includeScores);
    }

    public async Task<List<NodeDto>> QueryDependenciesAsync(
        string nodeId,
        DependencyDirection direction = DependencyDirection.Both,
        int maxDepth = -1,
        bool includeScores = false
    )
    {
        return await _repository.QueryDependenciesAsync(nodeId, direction, maxDepth, includeScores);
    }

    public async Task<List<NodeDto>> QueryPatternsAsync(
        AvecScores targetProfile,
        double threshold = 0.8
    )
    {
        return await _repository.QueryPatternsAsync(targetProfile, threshold);
    }

    public async Task<List<NodeDto>> SearchByNameAsync(string name, int limit = 10)
    {
        return await _repository.SearchByNameAsync(name, limit);
    }

    public async Task<List<NodeDto>> GetNodesWithHighFrictionAsync(
        double minFriction = 0.7,
        int limit = 20
    )
    {
        return await _repository.GetNodesWithHighFrictionAsync(minFriction, limit);
    }

    public async Task<List<NodeDto>> GetUnstableNodesAsync(
        double maxStability = 0.4,
        int limit = 20
    )
    {
        return await _repository.GetUnstableNodesAsync(maxStability, limit);
    }

    public async Task<ProjectStatsDto> GetProjectStatsAsync()
    {
        return await _repository.GetProjectStatsAsync();
    }
}
