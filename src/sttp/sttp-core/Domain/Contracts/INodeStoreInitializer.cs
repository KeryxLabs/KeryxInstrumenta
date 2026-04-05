namespace SttpMcp.Domain.Contracts;

public interface INodeStoreInitializer
{
    Task InitializeAsync(CancellationToken ct = default);
}