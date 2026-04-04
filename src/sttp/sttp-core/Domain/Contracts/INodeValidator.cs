using SttpMcp.Domain.Models;

namespace SttpMcp.Domain.Contracts;

public interface INodeValidator
{
    ValidationResult Validate(string rawNode);

    bool VerifyPsi(SttpNode node);
}