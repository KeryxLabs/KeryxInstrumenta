using AdaptiveCodecContextEngine.Models;
using Microsoft.Extensions.Logging;

public interface IAccQueryService
{
    Task<NodeDto?> GetNodeAsync(string nodeId);
    Task<NodeDto?> QueryRelationsAsync(string nodeId, bool includeScores = false);
    Task<List<NodeDto>> QueryDependenciesAsync(
        string nodeId,
        DependencyDirection direction = DependencyDirection.Both,
        int maxDepth = -1,
        bool includeScores = false);
    Task<List<NodeDto>> QueryPatternsAsync(AvecScores targetProfile, double threshold = 0.8);
    Task<List<NodeDto>> SearchByNameAsync(string name, int limit = 10);
    Task<List<NodeDto>> GetNodesWithHighFrictionAsync(double minFriction = 0.7, int limit = 20);
    Task<List<NodeDto>> GetUnstableNodesAsync(double maxStability = 0.4, int limit = 20);
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
        bool includeScores = false)
    {
        return await _repository.QueryDependenciesAsync(nodeId, direction, maxDepth, includeScores);
    }
    
    public async Task<List<NodeDto>> QueryPatternsAsync(AvecScores targetProfile, double threshold = 0.8)
    {
        return await _repository.QueryPatternsAsync(targetProfile, threshold);
    }
    
    public async Task<List<NodeDto>> SearchByNameAsync(string name, int limit = 10)
    {
        return await _repository.SearchByNameAsync(name, limit);
    }
    
    public async Task<List<NodeDto>> GetNodesWithHighFrictionAsync(double minFriction = 0.7, int limit = 20)
    {
        return await _repository.GetNodesWithHighFrictionAsync(minFriction, limit);
    }
    
    public async Task<List<NodeDto>> GetUnstableNodesAsync(double maxStability = 0.4, int limit = 20)
    {
        return await _repository.GetUnstableNodesAsync(maxStability, limit);
    }
    
    public async Task<ProjectStatsDto> GetProjectStatsAsync()
    {
        return await _repository.GetProjectStatsAsync();
    }
}

public record ProjectStatsDto
{
    public int TotalNodes { get; init; }
    public double AverageStability { get; init; }
    public double AverageLogic { get; init; }
    public double AverageFriction { get; init; }
    public double AverageAutonomy { get; init; }
}