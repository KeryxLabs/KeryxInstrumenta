using Microsoft.Extensions.Logging;
using SttpMcp.Domain.Contracts;
using SttpMcp.Domain.Models;
using SttpMcp.Parsing;

namespace SttpMcp.Application.Services;

public sealed class StoreContextService(INodeStore store, INodeValidator validator, ILogger<StoreContextService> logger)
{
    private readonly SttpNodeParser _parser = new();

    public async Task<StoreResult> StoreAsync(
        string node,
        string sessionId,
        CancellationToken ct = default)
    {
        var validation = validator.Validate(node);

        if (!validation.IsValid)
        {
            return new StoreResult
            {
                NodeId = string.Empty,
                Psi = 0f,
                Valid = false,
                ValidationError = $"{validation.Reason}: {validation.Error}"
            };
        }

        var parseResult = _parser.TryParse(node, sessionId);
        if (!parseResult.Success)
        {
            return new StoreResult
            {
                NodeId = string.Empty,
                Psi = 0f,
                Valid = false,
                ValidationError = $"ParseFailure: {parseResult.Error}"
            };
        }

        var parsed = parseResult.Node!;

        logger.LogInformation(
            "Parsed node - UserAvec.Psi: {UserPsi}, ModelAvec.Psi: {ModelPsi}, CompressionAvec.Psi: {CompPsi}",
            parsed.UserAvec.Psi,
            parsed.ModelAvec.Psi,
            parsed.CompressionAvec?.Psi);

        try
        {
            var nodeId = await store.StoreAsync(parsed, ct);

            return new StoreResult
            {
                NodeId = nodeId,
                Psi = parsed.Psi,
                Valid = true
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Store operation failed");
            return new StoreResult
            {
                NodeId = string.Empty,
                Psi = 0f,
                Valid = false,
                ValidationError = $"StoreFailure: {ex.Message}"
            };
        }
    }
}