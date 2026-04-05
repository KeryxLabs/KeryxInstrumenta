using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using SttpMcp.Application.Services;
using SttpMcp.Application.Validation;
using SttpMcp.Domain.Models;
using SttpMcp.Storage;

namespace SttpMcp.Tests.Services;

public class MonthlyRollupServiceTests
{
    private readonly InMemoryNodeStore _store = new();
    private readonly TreeSitterValidator _validator = new();

    [Fact]
    public async Task Should_Create_Monthly_Rollup_Using_First_Timeline_Node_As_Parent()
    {
        var ct = TestContext.Current.CancellationToken;
        var storeContext = new StoreContextService(_store, _validator, NullLogger<StoreContextService>.Instance);

        await storeContext.StoreAsync(BuildNode("first-session", "2026-03-05T10:20:00Z", 0.91f, 0.21f, 0.86f, 0.94f, 0.88f, 0.90f, 2.92f), "first-session", ct);
        await storeContext.StoreAsync(BuildNode("second-session", "2026-03-21T20:43:43Z", 0.82f, 0.28f, 0.87f, 0.63f, 0.91f, 0.88f, 2.60f), "second-session", ct);

        var service = new MonthlyRollupService(_store, _validator, NullLogger<MonthlyRollupService>.Instance);
        var result = await service.CreateAsync(new MonthlyRollupRequest
        {
            SessionId = "sttp-monthly-rollup-2026-04-04",
            StartUtc = DateTime.Parse("2026-03-01T00:00:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind),
            EndUtc = DateTime.Parse("2026-04-01T00:00:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind)
        }, ct);

        result.Success.ShouldBeTrue(result.Error);
        result.SourceNodes.ShouldBe(2);
        result.ParentReference.ShouldBe("first-session");
        result.RawNode.ShouldContain("parent_node: ref:first-session");
        result.UserAverage.Stability.ShouldBe(0.865f, 0.001f);
        result.RhoBands.High.ShouldBe(2);
        result.NodeId.ShouldNotBeNullOrWhiteSpace();
    }

    private static string BuildNode(
        string sessionId,
        string timestamp,
        float userStability,
        float userFriction,
        float userLogic,
        float userAutonomy,
        float rho,
        float kappa,
        float psi)
    {
        var avecPsi = userStability + userFriction + userLogic + userAutonomy;

        return string.Join("\n",
            $"⊕⟨ {{ trigger: manual, response_format: temporal_node, origin_session: \"{sessionId}\", compression_depth: 1, parent_node: null, prime: {{ attractor_config: {{ stability: {userStability}, friction: {userFriction}, logic: {userLogic}, autonomy: {userAutonomy} }}, context_summary: \"test node\", relevant_tier: raw, retrieval_budget: 3 }} }} ⟩",
            $"⦿⟨ {{ timestamp: \"{timestamp}\", tier: raw, session_id: \"{sessionId}\", user_avec: {{ stability: {userStability}, friction: {userFriction}, logic: {userLogic}, autonomy: {userAutonomy}, psi: {avecPsi} }}, model_avec: {{ stability: {userStability}, friction: {userFriction}, logic: {userLogic}, autonomy: {userAutonomy}, psi: {avecPsi} }} }} ⟩",
            "◈⟨ { test(.99): \"service test\" } ⟩",
            $"⍉⟨ {{ rho: {rho}, kappa: {kappa}, psi: {psi}, compression_avec: {{ stability: {userStability}, friction: {userFriction}, logic: {userLogic}, autonomy: {userAutonomy}, psi: {avecPsi} }} }} ⟩");
    }
}