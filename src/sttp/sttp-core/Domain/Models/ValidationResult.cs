namespace SttpMcp.Domain.Models;

public record ValidationResult
{
    public required bool IsValid { get; init; }
    public string? Error { get; init; }
    public required ValidationFailureReason Reason { get; init; }

    public static ValidationResult Ok() => new()
    {
        IsValid = true,
        Reason = ValidationFailureReason.None
    };

    public static ValidationResult Fail(
        string error,
        ValidationFailureReason reason) => new()
    {
        IsValid = false,
        Error = error,
        Reason = reason
    };
}