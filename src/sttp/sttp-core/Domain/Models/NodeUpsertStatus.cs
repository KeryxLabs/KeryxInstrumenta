namespace SttpMcp.Domain.Models;

public enum NodeUpsertStatus
{
    Created,
    Updated,
    Duplicate,
    Skipped
}