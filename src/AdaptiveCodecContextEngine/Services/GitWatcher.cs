using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Git;
using LibGit2Sharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class GitWatcher
{
    private readonly string _repoPath;
    private readonly Channel<GitEvent> _eventChannel;
    private readonly FileSystemWatcher _fsWatcher;
    private readonly ILogger<GitWatcher> _logger;
    private readonly HashSet<string> _relevantExtensions;

    private Repository? _repo;

    public GitWatcher(
        Channel<GitEvent> eventChannel,
        ILogger<GitWatcher> logger,
        IConfiguration configuration)
    {
        var accOptions = configuration.GetSection("Acc").Get<AccOptions>()
       ?? throw new InvalidOperationException("ACC configuration missing");
        _repoPath = accOptions.RepositoryPath;

        _eventChannel = eventChannel;
        _logger = logger;


        _fsWatcher = new FileSystemWatcher(_repoPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
                                            | NotifyFilters.DirectoryName // Often required for nested Linux files
                                | NotifyFilters.Attributes
                                | NotifyFilters.Size
        };

        var extensions = configuration.GetSection("Acc:FileExtensions").Get<string[]>();
        _relevantExtensions = extensions != null && extensions.Any()
            ? new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                        ".cs", ".ts", ".js", ".py", ".go", ".rs", ".java", ".cpp", ".c", ".h"
            };
    }

    public async Task StartAsync(CancellationToken ct)
    {

        // Load relevant file extensions from config

        _logger.LogInformation("GitWatcher configured for extensions: {Extensions}",
            string.Join(", ", _relevantExtensions));
        _logger.LogInformation("GitWatcher starting for repo: {RepoPath}", _repoPath);

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

        _logger.LogInformation("FileSystemWatcher enabled for: {RepoPath}", _repoPath);

        // Initial index of entire repo (runs async, doesn't block)
        _ = Task.Run(async () => await IndexRepository(ct), ct);

    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {

        // Filter by language extension
        if (!IsRelevantFile(e.FullPath))
        {
            _logger.LogTrace("Ignoring file: {FilePath}", e.FullPath);
            return;
        }

        _logger.LogDebug("File changed: {FilePath}", e.FullPath);

        _eventChannel.Writer.TryWrite(new GitEvent
        {
            Type = e.ChangeType == WatcherChangeTypes.Created ? GitEventType.Created : GitEventType.Modified,
            FilePath = e.FullPath,
            Timestamp = DateTime.UtcNow
        });
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (!IsRelevantFile(e.FullPath))
        {
            _logger.LogTrace("Ignoring renamed file: {FilePath}", e.FullPath);
            return;
        }

        _logger.LogDebug("File renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);

        _eventChannel.Writer.TryWrite(new GitEvent
        {
            Type = GitEventType.Renamed,
            FilePath = e.FullPath,
            OldPath = e.OldFullPath,
            Timestamp = DateTime.UtcNow
        });
    }

    private async Task IndexRepository(CancellationToken ct)
    {
        _logger.LogInformation("Starting initial repository index...");

        var fileCount = 0;

        try
        {
            var files = Directory.EnumerateFiles(_repoPath, "*.*", SearchOption.AllDirectories)
                .Where(IsRelevantFile);

            foreach (var file in files)
            {
                if (ct.IsCancellationRequested) break;

                _logger.LogDebug("Indexing file: {FilePath}", file);

                _eventChannel.Writer.TryWrite(new GitEvent
                {
                    Type = GitEventType.Initial,
                    FilePath = file,
                    Timestamp = DateTime.UtcNow
                });

                fileCount++;

                // Yield to avoid blocking
                if (fileCount % 100 == 0)
                {
                    _logger.LogInformation("Indexed {Count} files so far...", fileCount);
                    await Task.Delay(10, ct);
                }
            }

            _logger.LogInformation("Initial repository index complete. Indexed {Count} files.", fileCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial repository index");
        }
    }

    private bool IsRelevantFile(string path)
    {
        // Ignore git internals, node_modules, bin, obj, etc.
        if (path.Contains("/.git/") ||
            path.Contains("\\.git\\") ||
            path.Contains("/node_modules/") ||
            path.Contains("\\node_modules\\") ||
            path.Contains("/bin/") ||
            path.Contains("\\bin\\") ||
            path.Contains("/obj/") ||
            path.Contains("\\obj\\") ||
            path.Contains("/.vs/") ||
            path.Contains("\\.vs\\") ||
            path.Contains("/target/") ||  // Rust
            path.Contains("\\target\\"))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        return _relevantExtensions.Contains(extension);
    }

    public GitHistory ExtractHistory(string filePath)
    {
        if (_repo == null)
        {
            _logger.LogWarning("Repository not initialized, returning empty history");
            return new GitHistory
            {
                Created = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                TotalCommits = 0,
                Contributors = 0,
                AvgDaysBetweenChanges = 0
            };
        }

        var relativePath = Path.GetRelativePath(_repoPath, filePath);

        try
        {
            // Get commits that touched this file
            var commits = _repo.Commits
                .QueryBy(relativePath)
                .ToList();

            if (!commits.Any())
            {
                _logger.LogDebug("No git history found for {FilePath}", relativePath);
                return new GitHistory
                {
                    Created = File.GetCreationTimeUtc(filePath),
                    LastModified = File.GetLastWriteTimeUtc(filePath),
                    TotalCommits = 0,
                    Contributors = 0,
                    AvgDaysBetweenChanges = 0,
                    RecentFrequency = "low"
                };
            }

            var firstCommit = commits.Last().Commit;
            var lastCommit = commits.First().Commit;

            // Count unique contributors
            var contributors = commits
                .Select(c => c.Commit.Author.Email)
                .Distinct()
                .Count();

            // Calculate average time between changes
            var avgDays = 0.0;
            if (commits.Count > 1)
            {
                var totalDays = (lastCommit.Author.When - firstCommit.Author.When).TotalDays;
                avgDays = totalDays / (commits.Count - 1);
            }

            // Determine recent frequency (last 30 days)
            var recentCommits = commits
                .Where(c => c.Commit.Author.When > DateTimeOffset.UtcNow.AddDays(-30))
                .Count();

            var frequency = recentCommits switch
            {
                0 => "low",
                1 => "low",
                2 or 3 => "medium",
                _ => "high"
            };

            return new GitHistory
            {
                Created = firstCommit.Author.When.UtcDateTime,
                LastModified = lastCommit.Author.When.UtcDateTime,
                TotalCommits = commits.Count,
                Contributors = contributors,
                AvgDaysBetweenChanges = avgDays,
                RecentFrequency = frequency
            };

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting git history for {FilePath}", relativePath);
            return new GitHistory
            {
                Created = File.GetCreationTimeUtc(filePath),
                LastModified = File.GetLastWriteTimeUtc(filePath),
                TotalCommits = 0,
                Contributors = 0,
                AvgDaysBetweenChanges = 0,
                RecentFrequency = "low"
            };
        }
    }

    public void Dispose()
    {
        _fsWatcher?.Dispose();
        _repo?.Dispose();
    }
}