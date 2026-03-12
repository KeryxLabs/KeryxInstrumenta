using AdaptiveCodecContextEngine.Models.Git;
using LibGit2Sharp;

public class GitWatcher
{
    private readonly string _repoPath;
    private readonly Channel<GitEvent> _eventChannel;
    private readonly FileSystemWatcher _fsWatcher;
    private Repository? _repo;
    
    public GitWatcher(string repoPath, Channel<GitEvent> eventChannel)
    {
        _repoPath = repoPath;
        _eventChannel = eventChannel;
        _fsWatcher = new FileSystemWatcher(repoPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };
    }
    
    public async Task StartAsync(CancellationToken ct)
    {
        _repo = new Repository(_repoPath);
        
        // Hook up file system events
        _fsWatcher.Changed += OnFileChanged;
        _fsWatcher.Created += OnFileChanged;
        _fsWatcher.Deleted += OnFileChanged;
        _fsWatcher.Renamed += OnFileRenamed;
        
        _fsWatcher.EnableRaisingEvents = true;
        
        // Initial index of entire repo
        await IndexRepository(ct);
        
        // Keep watching
        await Task.Delay(Timeout.Infinite, ct);
    }
    
    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Filter by language extension
        if (!IsRelevantFile(e.FullPath)) return;
        
        _eventChannel.Writer.TryWrite(new GitEvent
        {
            Type = GitEventType.Modified,
            FilePath = e.FullPath,
            Timestamp = DateTime.UtcNow
        });
    }
    
    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (!IsRelevantFile(e.FullPath)) return;
        
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
        // Walk all files in repo
        var files = Directory.EnumerateFiles(_repoPath, "*.*", SearchOption.AllDirectories)
            .Where(IsRelevantFile);
        
        foreach (var file in files)
        {
            if (ct.IsCancellationRequested) break;
            
            _eventChannel.Writer.TryWrite(new GitEvent
            {
                Type = GitEventType.Initial,
                FilePath = file,
                Timestamp = DateTime.UtcNow
            });
        }
    }
    
    private bool IsRelevantFile(string path)
    {
        // Filter by configured language extensions
        var extension = Path.GetExtension(path).ToLowerInvariant();
        
        // Ignore git internals, node_modules, bin, obj, etc.
        if (path.Contains("/.git/") || 
            path.Contains("/node_modules/") ||
            path.Contains("/bin/") ||
            path.Contains("/obj/")) 
            return false;
        
        // TODO: Load from config based on LSP language
        return extension is ".cs" or ".ts" or ".js" or ".py" or ".go";
    }
    
    public GitHistory ExtractHistory(string filePath)
    {
        if (_repo == null) throw new InvalidOperationException("Repository not initialized");
        
        var relativePath = Path.GetRelativePath(_repoPath, filePath);
        
        // Get commits that touched this file
        var commits = _repo.Commits
            .QueryBy(relativePath)
            .ToList();
        
        if (!commits.Any())
        {
            return new GitHistory
            {
                Created = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                TotalCommits = 0,
                Contributors = 0,
                AvgDaysBetweenChanges = 0
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
    
    public void Dispose()
    {
        _fsWatcher?.Dispose();
        _repo?.Dispose();
    }
}

