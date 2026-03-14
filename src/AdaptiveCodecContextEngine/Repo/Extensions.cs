using AdaptiveCodecContextEngine.Models.Surreal;

public static class DbHelperExtensions
{
    extension(IEnumerable<NodeUpdate> updates)
    {
        public IEnumerable<Dictionary<string, object?>> AsInsertable()
        {
            List<Dictionary<string, object?>> nodes = [];
            foreach (var update in updates)
            {
                var node = new Dictionary<string, object?>
                {
                    ["id"] = update.NodeId,
                    ["node_id"] = update.NodeId,
                    ["type"] = update.Type,
                    ["language"] = update.Language,
                    ["name"] = update.Name,
                    ["file_path"] = update.FilePath,
                    ["line_start"] = update.LineStart,
                    ["line_end"] = update.LineEnd,
                    ["lines_of_code"] = update.LinesOfCode ?? 0,
                    ["cyclomatic_complexity"] = update.CyclomaticComplexity ?? 0,
                    ["parameters"] = update.Parameters ?? 0,
                    ["git_total_commits"] = update.GitHistory?.TotalCommits ?? 0,
                    ["git_contributors"] = update.GitHistory?.Contributors ?? 0,
                    ["git_avg_days_between_changes"] = (double)(update.GitHistory?.AvgDaysBetweenChanges ?? 0),
                    ["git_recent_frequency"] = update.GitHistory?.RecentFrequency ?? "low",
                    ["test_covered"] = update.TestCoverage?.Covered ?? false,
                    ["test_line_coverage"] = (double)(update.TestCoverage?.LineCoverage ?? 0),
                    ["test_branch_coverage"] = (double)(update.TestCoverage?.BranchCoverage ?? 0),
                    ["test_count"] = update.TestCoverage?.TestCount ?? 0
                };

                if (update.Namespace is not null)
                    node["namespace"] = (object?)update.Namespace;
                if (update.Signature is not null)
                    node["signature"] = (object?)update.Signature;
                if (update.ReturnType is not null)
                    node["return_type"] = (object?)update.ReturnType;
                if (update.GitHistory?.Created is not null)
                    node["git_created"] = (object?)update.GitHistory?.Created;
                if (update.GitHistory?.LastModified is not null)
                    node["git_last_modified"] = (object?)update.GitHistory?.LastModified;

                nodes.Add(node);
            }
            return nodes;
        }
    }
}