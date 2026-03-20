using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class AccHostedService : IHostedService
{
    private readonly SurrealDbRepository _repository;
    private readonly GitWatcher _gitWatcher;
    private readonly MetricsCollector _metricsCollector;
    private readonly ILogger<AccHostedService> _logger;

    public AccHostedService(
        SurrealDbRepository repository,
        GitWatcher gitWatcher,
        MetricsCollector metricsCollector,
        ILogger<AccHostedService> logger
    )
    {
        _repository = repository;
        _gitWatcher = gitWatcher;
        _metricsCollector = metricsCollector;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ACC starting up...");

        try
        {
            // Initialize database
            _logger.LogInformation("Initializing SurrealDB...");
            await _repository.InitializeAsync();

            // Start git watcher
            _logger.LogInformation("Starting Git watcher...");
            await _gitWatcher.StartAsync(stoppingToken);

            // Start metrics collector
            _logger.LogInformation("Starting metrics collector...");
            await _metricsCollector.StartAsync(stoppingToken);

            _logger.LogInformation("ACC is running. Press Ctrl+C to stop.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ACC shutting down...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ACC encountered an error");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Service...");
        return Task.CompletedTask;
    }
}
