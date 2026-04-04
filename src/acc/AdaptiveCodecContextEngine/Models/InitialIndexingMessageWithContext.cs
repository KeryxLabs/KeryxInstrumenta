using AdaptiveCodecContextEngine.Models.Git;

namespace AdaptiveCodecContextEngine.Models;

public readonly record struct InitialIndexingMessageWithContext(
    string FilePath,
    Dictionary<string, GitHistory> GitHistories,
    ActivityContext? Context
);
