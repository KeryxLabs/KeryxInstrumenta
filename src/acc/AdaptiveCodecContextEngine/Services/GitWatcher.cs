using AdaptiveCodecContextEngine.Diagnostics;
using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Git;
using LibGit2Sharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class GitWatcher : IDisposable
{
    private readonly string _repoPath;
    private readonly Channel<GitEventWithContext> _eventChannel;
    private readonly FileSystemWatcher _fsWatcher;
    private readonly ILogger<GitWatcher> _logger;
    private readonly HashSet<string> _relevantExtensions;
    private readonly SurrealDbRepository _repository;

    private Repository? _repo;
    private readonly AdaptiveContextInstrumentation _instrumentation;
    public string RepoPath => _repoPath;
    public GitWatcher(
        Channel<GitEventWithContext> eventChannel,
        SurrealDbRepository repository,
        ILogger<GitWatcher> logger,
        IConfiguration configuration,
        AdaptiveContextInstrumentation instrumentation
    )
    {
        var accOptions =
            configuration.GetSection("Acc").Get<AccOptions>()
            ?? throw new InvalidOperationException("ACC configuration missing");
        _repoPath = accOptions.RepositoryPath;

        _eventChannel = eventChannel;
        _logger = logger;
        _instrumentation = instrumentation;
        _repository = repository;

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
            _repo = new Repository(_repoPath);
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
            var files = Directory
                .EnumerateFiles(_repoPath, "*.*", SearchOption.AllDirectories)
                .Where(IsRelevantFile);

            foreach (var file in files)
            {
                using var fileActivity = _instrumentation.ActivitySource.StartActivity(
                    "FileIndex",
                    ActivityKind.Producer,
                    parentContext: activity?.Context ?? new()
                );
                if (ct.IsCancellationRequested)
                    break;

                fileActivity?.SetTag("index.file", file);

                _eventChannel.Writer.TryWrite(
                    new GitEventWithContext(
                        new GitEvent
                        {
                            Type = GitEventType.Initial,
                            FilePath = file,
                            Timestamp = DateTime.UtcNow,
                        },
                        fileActivity?.Context
                    )
                );

                fileCount++;

                activity?.AddEvent(new("index.progress", tags: new() { ["count"] = fileCount }));
            }

            activity?.SetTag("index.total", fileCount);

            // Persist last indexed timestamp so restarts are idempotent
            try
            {
                await _repository.SetLastIndexedAtAsync(_repoPath, DateTime.UtcNow, ct);
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

    private bool IsRelevantFile(string path)
    {
        // Ignore git internals, node_modules, bin, obj, etc.
        if (
            path.Contains("/.git/")
            || path.Contains("\\.git\\")
            || path.Contains("/node_modules/")
            || path.Contains("\\node_modules\\")
            || path.Contains("/bin/")
            || path.Contains("\\bin\\")
            || path.Contains("/obj/")
            || path.Contains("\\obj\\")
            || path.Contains("/.vs/")
            || path.Contains("\\.vs\\")
            || path.Contains("/target/")
            || // Rust
            path.Contains("\\target\\")
        )
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        return _relevantExtensions.Contains(extension);
    }

    public GitHistory ExtractHistory(string filePath)
    {
        using var activity = _instrumentation.ActivitySource.StartActivity(nameof(ExtractHistory));

        if (_repo == null)
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

        var relativePath = Path.GetRelativePath(_repoPath, filePath);
        activity?.SetTag("history.file", relativePath);

        try
        {
            // Get commits that touched this file
            var commits = _repo.Commits.QueryBy(relativePath).ToList();

            activity?.SetTag("history.commits.count", commits.Count);

            if (!commits.Any())
            {
                activity?.AddEvent(new("history.none"));
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

            var firstCommit = commits.Last().Commit;
            var lastCommit = commits.First().Commit;

            // Count unique contributors
            var contributors = commits.Select(c => c.Commit.Author.Email).Distinct().Count();

            // Calculate average time between changes
            var avgDays = 0.0;
            if (commits.Count > 1)
            {
                var totalDays = (lastCommit.Author.When - firstCommit.Author.When).TotalDays;
                avgDays = totalDays / (commits.Count - 1);
            }

            // Determine recent frequency (last 30 days)
            var recentCommits = commits.Count(c =>
                c.Commit.Author.When > DateTimeOffset.UtcNow.AddDays(-30)
            );

            var frequency = recentCommits switch
            {
                0 => "low",
                1 => "low",
                2 or 3 => "medium",
                _ => "high",
            };

            activity?.SetTag("history.contributors", contributors);
            activity?.SetTag("history.commits.recent", recentCommits);
            activity?.SetTag("history.commits.frequency", frequency);

            return new GitHistory
            {
                Created = firstCommit.Author.When.UtcDateTime,
                LastModified = lastCommit.Author.When.UtcDateTime,
                TotalCommits = commits.Count,
                Contributors = contributors,
                AvgDaysBetweenChanges = avgDays,
                RecentFrequency = frequency,
            };
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
        using var activity = _instrumentation.ActivitySource.StartActivity(nameof(Dispose));
        activity?.AddEvent(new("gitwatcher.dispose"));

        _fsWatcher?.Dispose();
        _repo?.Dispose();
    }
}
