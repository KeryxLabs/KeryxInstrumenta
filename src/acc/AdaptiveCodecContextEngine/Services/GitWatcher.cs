using System.Text;
using AdaptiveCodecContextEngine.Diagnostics;
using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Git;
using LibGit2Sharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public class GitWatcher : IDisposable
{
    private readonly string _repoPath;
    private readonly Channel<GitEventWithContext> _eventChannel;
    private readonly Channel<InitialIndexingMessageWithContext> _initialIndexChannel;

    private readonly FileSystemWatcher _fsWatcher;
    private readonly FileSystemWatcher _gitHeadWatcher;
    private readonly ILogger<GitWatcher> _logger;
    private readonly HashSet<string> _relevantExtensions;
    private readonly SurrealDbRepository _repository;
    private readonly IServiceProvider _serviceProvider;
    private string? _trackingBranch;
    private readonly AdaptiveContextInstrumentation _instrumentation;
    public string RepoPath => _repoPath;

    public GitWatcher(
        Channel<GitEventWithContext> eventChannel,
        Channel<InitialIndexingMessageWithContext> initialIndexChannel,
        SurrealDbRepository repository,
        ILogger<GitWatcher> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        AdaptiveContextInstrumentation instrumentation
    )
    {
        var accOptions =
            configuration.GetSection("Acc").Get<AccOptions>()
            ?? throw new InvalidOperationException("ACC configuration missing");
        _repoPath = accOptions.RepositoryPath;

        _eventChannel = eventChannel;
        _initialIndexChannel = initialIndexChannel;
        _logger = logger;
        _instrumentation = instrumentation;
        _repository = repository;
        _serviceProvider = serviceProvider;

        _fsWatcher = new FileSystemWatcher(_repoPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter =
                NotifyFilters.LastWrite
                | NotifyFilters.FileName
                | NotifyFilters.CreationTime
                | NotifyFilters.DirectoryName // Often required for nested Linux files
                | NotifyFilters.Attributes
                | NotifyFilters.Size,
        };
        _gitHeadWatcher = new FileSystemWatcher(Path.Join(_repoPath, ".git"))
        {
            NotifyFilter = NotifyFilters.LastWrite,
            Filter = "HEAD",
        };

        var extensions = configuration.GetSection("Acc:FileExtensions").Get<string[]>();
        _relevantExtensions =
            extensions != null && extensions.Any()
                ? new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".cs",
                    ".ts",
                    ".js",
                    ".py",
                    ".go",
                    ".rs",
                    ".java",
                    ".cpp",
                    ".c",
                    ".h",
                };
    }

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            var git = _serviceProvider.GetRequiredKeyedService<GitClient>(GitClient.ServiceName);
            _trackingBranch = await git.GetRepoFriendlyName(_repoPath, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open git repository at {RepoPath}", _repoPath);
            throw;
        }

        // Hook up file system events
        _fsWatcher.Changed += OnFileChanged;
        _fsWatcher.Created += OnFileChanged;
        _fsWatcher.Deleted += OnFileChanged;
        _fsWatcher.Renamed += OnFileRenamed;
        _fsWatcher.Error += (s, e) => _logger.LogError(e.GetException(), "Watcher Error!");
        _fsWatcher.EnableRaisingEvents = true;

        DateTime _lastRead = DateTime.MinValue;

        //Git Head watcher
        _gitHeadWatcher.Changed += async (s, e) =>
        {
            var git = _serviceProvider.GetRequiredKeyedService<GitClient>(GitClient.ServiceName);
            using var repo = new Repository(_repoPath);
            var currentBranch = await git.GetRepoFriendlyName(_repoPath, ct);

            if (currentBranch != _trackingBranch)
            {
                _logger.LogInformation(
                    "Branch changed from {trackingBranch} to {currentBranch}. Restarting...",
                    _trackingBranch,
                    currentBranch
                );

                RestartApplication();
            }
        };
        _gitHeadWatcher.EnableRaisingEvents = true;

        _logger.LogTrace("Git Watcher Enabled");

        // Initial index of entire repo (runs async, doesn't block)
        _ = Task.Run(
            async () =>
            {
                try
                {
                    var last = await _repository.GetLastIndexedAtAsync(_repoPath, ct);
                    if (last.HasValue && (DateTime.UtcNow - last.Value) < TimeSpan.FromSeconds(30))
                    {
                        _logger.LogInformation(
                            "Skipping initial repository index; last indexed at {LastIndexedAt}",
                            last.Value
                        );
                        return;
                    }

                    await IndexRepository(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during conditional initial index");
                }
            },
            ct
        );
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Filter by language extension
        if (!IsRelevantFile(e.FullPath))
        {
            _logger.LogTrace("Ignoring file: {FilePath}", e.FullPath);
            return;
        }
        using var activity = _instrumentation.ActivitySource.StartActivity(nameof(OnFileChanged));
        activity?.SetTag("file.path", e.FullPath);
        activity?.SetTag("file.change", e.ChangeType.ToString());

        var written = _eventChannel.Writer.TryWrite(
            new GitEventWithContext(
                new GitEvent
                {
                    Type =
                        e.ChangeType == WatcherChangeTypes.Created
                            ? GitEventType.Created
                            : GitEventType.Modified,
                    FilePath = e.FullPath,
                    Timestamp = DateTime.UtcNow,
                },
                activity?.Context
            )
        );
        activity?.SetTag("event.enqueued", written);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (!IsRelevantFile(e.FullPath))
        {
            _logger.LogTrace("Ignoring renamed file: {FilePath}", e.FullPath);
            return;
        }
        using var activity = _instrumentation.ActivitySource.StartActivity(nameof(OnFileRenamed));
        activity?.SetTag("file.old", e.OldFullPath);
        activity?.SetTag("file.new", e.FullPath);

        var written = _eventChannel.Writer.TryWrite(
            new GitEventWithContext(
                new GitEvent
                {
                    Type = GitEventType.Renamed,
                    FilePath = e.FullPath,
                    OldPath = e.OldFullPath,
                    Timestamp = DateTime.UtcNow,
                },
                activity?.Context
            )
        );
        activity?.SetTag("event.enqueued", written);
    }

    private void RestartApplication()
    {
        string? exePath = Process.GetCurrentProcess()?.MainModule?.FileName;

        if (string.IsNullOrEmpty(exePath))
        {
            _logger.LogError("Unable to get main module file name. Shutting down engine.");
            Environment.Exit(0);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true, // Ensures it starts as a fresh top-level process
            WorkingDirectory = Environment.CurrentDirectory,
        };
        startInfo.Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1));
        _logger.LogInformation("Restarting Service in new shell.");

        Process.Start(startInfo);
        Environment.Exit(0);
    }

    private async Task IndexRepository(CancellationToken ct)
    {
        using var activity = _instrumentation.ActivitySource.StartActivity(
            nameof(IndexRepository),
            ActivityKind.Producer,
            parentContext: new()
        );
        activity?.SetTag("gitwatcher.repo", _repoPath);
        activity?.SetTag("gitwatcher.extensions.count", _relevantExtensions.Count);

        _logger.LogInformation("Starting initial repository index...");

        var fileCount = 0;

        try
        {
            var client = _serviceProvider.GetRequiredKeyedService<GitClient>(GitClient.ServiceName);

            var files = Directory
                .EnumerateFiles(_repoPath, "*.*", SearchOption.AllDirectories)
                .Where(IsRelevantFile);

            var histories = await client.ExtractHistory(_repoPath, ct: ct);
            if (histories is null)
            {
                _logger.LogWarning("Unable to start indexing.");
                return;
            }

            Dictionary<string, GitHistory> events = [];
            var today = DateTime.Now;
            foreach (var file in files)
            {
                if (ct.IsCancellationRequested)
                    break;

                var relativePath = Path.GetRelativePath(_repoPath, file);

                _logger.LogInformation("Processing {relativePath}", relativePath);

                if (histories.TryGetValue(relativePath, out var history))
                {
                    events.TryAdd(relativePath, history);
                }
                else
                {
                    var freshHistory = new GitHistory()
                    {
                        Created = today,
                        LastModified = today,
                        TotalCommits = 0,
                    };
                    events.TryAdd(relativePath, freshHistory);
                    _logger.LogWarning(
                        "No git history found for {relativePath} starting with {history}",
                        relativePath,
                        freshHistory
                    );
                }

                fileCount++;
            }

            activity?.SetTag("index.total", fileCount);

            var initialRequest = new InitialIndexingMessageWithContext(
                _repoPath,
                events,
                activity?.Context
            );

            await _initialIndexChannel.Writer.WriteAsync(initialRequest, cancellationToken: ct);

            // Persist last indexed timestamp so restarts are idempotent
            try
            {
                await _repository.SetLastIndexedAtAsync(_repoPath, today, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist last indexed timestamp");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial repository index");
            activity?.AddEvent(new("index.error", tags: new() { ["exception"] = ex.Message }));
        }
    }

    // private bool IsRelevantFile(string path)
    // {
    //     // Ignore git internals, node_modules, bin, obj, etc.
    //     if (
    //         path.Contains("/.git/")
    //         || path.Contains("\\.git\\")
    //         || path.Contains("/node_modules/")
    //         || path.Contains("\\node_modules\\")
    //         || path.Contains("/bin/")
    //         || path.Contains("\\bin\\")
    //         || path.Contains("/obj/")
    //         || path.Contains("\\obj\\")
    //         || path.Contains("/.vs/")
    //         || path.Contains("\\.vs\\")
    //         || path.Contains("/target/")
    //         || // Rust
    //         path.Contains("\\target\\")
    //     )
    //     {
    //         return false;
    //     }

    //     var extension = Path.GetExtension(path);
    //     return _relevantExtensions.Contains(extension);
    // }

    private bool IsRelevantFile(string path)
    {
        ReadOnlySpan<char> pathSpan = path.AsSpan();

        if (
            pathSpan.Contains("/.git/", StringComparison.Ordinal)
            || pathSpan.Contains("\\.git\\", StringComparison.Ordinal)
            || pathSpan.Contains("/node_modules/", StringComparison.Ordinal)
            || pathSpan.Contains("\\node_modules\\", StringComparison.Ordinal)
            || pathSpan.Contains("/bin/", StringComparison.Ordinal)
            || pathSpan.Contains("\\bin\\", StringComparison.Ordinal)
            || pathSpan.Contains("/obj/", StringComparison.Ordinal)
            || pathSpan.Contains("\\obj\\", StringComparison.Ordinal)
            || pathSpan.Contains("/target/", StringComparison.Ordinal)
            || pathSpan.Contains("\\target\\", StringComparison.Ordinal)
            || pathSpan.Contains("/.vs/", StringComparison.Ordinal)
            || pathSpan.Contains("\\.vs\\", StringComparison.Ordinal)
        )
        {
            return false;
        }

        ReadOnlySpan<char> extSpan = Path.GetExtension(pathSpan);
        var lookup = _relevantExtensions.GetAlternateLookup<ReadOnlySpan<char>>();
        return lookup.Contains(extSpan);
    }

    public async Task<GitHistory> ExtractFileHistoryAsync(
        string relativePath,
        CancellationToken cancellationToken
    )
    {
        using var activity = _instrumentation.ActivitySource.StartActivity(
            nameof(ExtractFileHistoryAsync)
        );

        if (string.IsNullOrWhiteSpace(_trackingBranch))
        {
            _logger.LogWarning("Repository not initialized, returning empty history");
            activity?.SetTag("repo.initialized", false);
            return new GitHistory
            {
                Created = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                TotalCommits = 0,
                Contributors = 0,
                AvgDaysBetweenChanges = 0,
            };
        }

        var filePath = Path.GetFullPath(relativePath, _repoPath);

        activity?.SetTag("history.file", relativePath);

        try
        {
            var client = _serviceProvider.GetRequiredKeyedService<GitClient>(GitClient.ServiceName);

            var clientRes = await client.ExtractHistory(_repoPath, relativePath, cancellationToken);
            var history = clientRes?.FirstOrDefault();
            if (history is null)
            {
                activity?.AddEvent(new("history.none"));
                return new GitHistory
                {
                    Created = File.GetCreationTimeUtc(filePath),
                    LastModified = File.GetLastWriteTimeUtc(filePath),
                };
            }

            return history.Value.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting git history for {FilePath}", relativePath);
            activity?.AddEvent(new("history.error", tags: new() { ["exception"] = ex.Message }));
            return new GitHistory
            {
                Created = File.GetCreationTimeUtc(filePath),
                LastModified = File.GetLastWriteTimeUtc(filePath),
                TotalCommits = 0,
                Contributors = 0,
                AvgDaysBetweenChanges = 0,
                RecentFrequency = "low",
            };
        }
    }

    public void Dispose()
    {
        _fsWatcher?.Dispose();
    }
}
