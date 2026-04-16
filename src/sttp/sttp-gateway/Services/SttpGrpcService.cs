using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using SttpGateway.Grpc;
using SttpMcp.Application.Services;
using SttpMcp.Domain.Models;

namespace SttpGateway.Services;

public sealed class SttpGrpcService(
    CalibrationService calibrationService,
    StoreContextService storeContextService,
    ContextQueryService contextQueryService,
    MoodCatalogService moodCatalogService,
    MonthlyRollupService monthlyRollupService,
    RekeyScopeService rekeyScopeService)
    : SttpGatewayService.SttpGatewayServiceBase
{
    private const string TenantHeader = "x-tenant-id";
    private static readonly string[] TenantHeaders = ["x-resonantia-tenant", "x-tenant-id", "x-tenant"];
    private const string DefaultTenant = "default";
    private const string TenantScopePrefix = "tenant:";
    private const string TenantScopeSeparator = "::session:";
    private const int TenantScanLimit = 200;

    public override async Task<CalibrateSessionReply> CalibrateSession(CalibrateSessionRequest request, ServerCallContext context)
    {
        var tenant = ResolveGrpcTenant(context.RequestHeaders);
        var result = await calibrationService.CalibrateAsync(
            ScopeSessionId(tenant, request.SessionId),
            request.Stability,
            request.Friction,
            request.Logic,
            request.Autonomy,
            string.IsNullOrWhiteSpace(request.Trigger) ? "manual" : request.Trigger,
            context.CancellationToken);

        var reply = new CalibrateSessionReply
        {
            PreviousAvec = ToGrpc(result.PreviousAvec),
            Delta = result.Delta,
            DriftClassification = result.DriftClassification.ToString(),
            Trigger = result.Trigger,
            IsFirstCalibration = result.IsFirstCalibration
        };

        reply.TriggerHistory.AddRange(result.TriggerHistory);
        return reply;
    }

    public override async Task<StoreContextReply> StoreContext(StoreContextRequest request, ServerCallContext context)
    {
        var tenant = ResolveGrpcTenant(context.RequestHeaders);
        var result = await storeContextService.StoreAsync(
            request.Node,
            ScopeSessionId(tenant, request.SessionId),
            context.CancellationToken);

        return new StoreContextReply
        {
            NodeId = result.NodeId,
            Psi = result.Psi,
            Valid = result.Valid,
            ValidationError = result.ValidationError ?? string.Empty
        };
    }

    public override async Task<GetContextReply> GetContext(GetContextRequest request, ServerCallContext context)
    {
        var tenant = ResolveGrpcTenant(context.RequestHeaders);
        var result = await contextQueryService.GetContextAsync(
            ScopeSessionId(tenant, request.SessionId),
            request.Stability,
            request.Friction,
            request.Logic,
            request.Autonomy,
            request.Limit <= 0 ? 5 : request.Limit,
            context.CancellationToken);

        var reply = new GetContextReply
        {
            Retrieved = result.Retrieved,
            PsiRange = ToGrpc(result.PsiRange)
        };

        reply.Nodes.AddRange(result.Nodes.Select(node => NormalizeNodeForTenant(node, tenant)).Where(node => node is not null).Select(node => ToGrpc(node!)));
        reply.Retrieved = reply.Nodes.Count;
        return reply;
    }

    public override async Task<ListNodesReply> ListNodes(ListNodesRequest request, ServerCallContext context)
    {
        var tenant = ResolveGrpcTenant(context.RequestHeaders);
        var requestedLimit = Math.Clamp(request.Limit <= 0 ? 50 : request.Limit, 1, TenantScanLimit);
        var scopedSessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? null
            : ScopeSessionId(tenant, request.SessionId);
        var backendLimit = scopedSessionId is null ? TenantScanLimit : requestedLimit;

        var result = await contextQueryService.ListNodesAsync(
            backendLimit,
            scopedSessionId,
            context.CancellationToken);

        var nodes = result.Nodes
            .Select(node => NormalizeNodeForTenant(node, tenant))
            .Where(node => node is not null)
            .Take(requestedLimit)
            .Select(node => node!)
            .ToList();

        var reply = new ListNodesReply
        {
            Retrieved = nodes.Count
        };

        reply.Nodes.AddRange(nodes.Select(ToGrpc));
        return reply;
    }

    public override async Task<GetMoodsReply> GetMoods(GetMoodsRequest request, ServerCallContext context)
    {
        var result = await moodCatalogService.GetAsync(
            request.TargetMood,
            request.Blend,
            request.CurrentStability,
            request.CurrentFriction,
            request.CurrentLogic,
            request.CurrentAutonomy);

        var reply = new GetMoodsReply
        {
            ApplyGuide = result.ApplyGuide
        };

        reply.Presets.AddRange(result.Presets.Select(p => new Grpc.MoodPreset
        {
            Name = p.Name,
            Description = p.Description,
            Avec = ToGrpc(p.Avec)
        }));

        if (result.SwapPreview is not null)
        {
            reply.SwapPreview = new Grpc.MoodSwapPreview
            {
                TargetMood = result.SwapPreview.TargetMood,
                Blend = result.SwapPreview.Blend,
                Current = ToGrpc(result.SwapPreview.Current),
                Target = ToGrpc(result.SwapPreview.Target),
                Blended = ToGrpc(result.SwapPreview.Blended)
            };
        }

        return reply;
    }

    public override async Task<BatchRekeyReply> BatchRekey(BatchRekeyRequest request, ServerCallContext context)
    {
        if (request.NodeIds.Count == 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "node_ids must contain at least one value"));

        if (string.IsNullOrWhiteSpace(request.TargetSessionId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "target_session_id cannot be empty"));

        var metadataTenant = ResolveGrpcTenant(context.RequestHeaders);
        var targetTenant = NormalizeTenantValue(request.TargetTenantId) ?? metadataTenant;
        var scopedTargetSession = ScopeSessionId(targetTenant, request.TargetSessionId.Trim());

        var result = await rekeyScopeService.RekeyAsync(
            request.NodeIds,
            targetTenant,
            scopedTargetSession,
            request.HasDryRun ? request.DryRun : true,
            request.HasAllowMerge ? request.AllowMerge : false,
            context.CancellationToken);

        return ToGrpc(result);
    }

    public override async Task<CreateMonthlyRollupReply> CreateMonthlyRollup(CreateMonthlyRollupRequest request, ServerCallContext context)
    {
        var tenant = ResolveGrpcTenant(context.RequestHeaders);
        var monthlyRequest = new MonthlyRollupRequest
        {
            SessionId = ScopeSessionId(tenant, request.SessionId),
            StartUtc = request.StartUtc.ToDateTime(),
            EndUtc = request.EndUtc.ToDateTime(),
            SourceSessionId = string.IsNullOrWhiteSpace(request.SourceSessionId)
                ? null
                : ScopeSessionId(tenant, request.SourceSessionId),
            ParentNodeId = string.IsNullOrWhiteSpace(request.ParentNodeId) ? null : request.ParentNodeId,
            Persist = request.Persist,
            Limit = request.Limit <= 0 ? 5000 : request.Limit
        };

        var result = await monthlyRollupService.CreateAsync(monthlyRequest, context.CancellationToken);

        return new CreateMonthlyRollupReply
        {
            Success = result.Success,
            NodeId = result.NodeId,
            RawNode = result.RawNode,
            Error = result.Error ?? string.Empty,
            SourceNodes = result.SourceNodes,
            ParentReference = result.ParentReference ?? string.Empty,
            UserAverage = ToGrpc(result.UserAverage),
            ModelAverage = ToGrpc(result.ModelAverage),
            CompressionAverage = ToGrpc(result.CompressionAverage),
            RhoRange = ToGrpc(result.RhoRange),
            KappaRange = ToGrpc(result.KappaRange),
            PsiRange = ToGrpc(result.PsiRange),
            RhoBands = ToGrpc(result.RhoBands),
            KappaBands = ToGrpc(result.KappaBands)
        };
    }

    private static Grpc.AvecState ToGrpc(SttpMcp.Domain.Models.AvecState value) => new()
    {
        Stability = value.Stability,
        Friction = value.Friction,
        Logic = value.Logic,
        Autonomy = value.Autonomy,
        Psi = value.Psi
    };

    private static Grpc.SttpNode ToGrpc(SttpMcp.Domain.Models.SttpNode value)
    {
        var node = new Grpc.SttpNode
        {
            Raw = value.Raw,
            SessionId = DisplaySessionId(value.SessionId),
            Tier = value.Tier,
            Timestamp = Timestamp.FromDateTime(value.Timestamp.ToUniversalTime()),
            CompressionDepth = value.CompressionDepth,
            UserAvec = ToGrpc(value.UserAvec),
            ModelAvec = ToGrpc(value.ModelAvec),
            Rho = value.Rho,
            Kappa = value.Kappa,
            Psi = value.Psi
        };

        if (!string.IsNullOrWhiteSpace(value.ParentNodeId))
            node.ParentNodeId = value.ParentNodeId;

        if (value.CompressionAvec is not null)
            node.CompressionAvec = ToGrpc(value.CompressionAvec);

        return node;
    }

    private static Grpc.PsiRange ToGrpc(SttpMcp.Domain.Models.PsiRange value) => new()
    {
        Min = value.Min,
        Max = value.Max,
        Average = value.Average
    };

    private static Grpc.NumericRange ToGrpc(SttpMcp.Domain.Models.NumericRange value) => new()
    {
        Min = value.Min,
        Max = value.Max,
        Average = value.Average
    };

    private static Grpc.ConfidenceBandSummary ToGrpc(SttpMcp.Domain.Models.ConfidenceBandSummary value) => new()
    {
        Low = value.Low,
        Medium = value.Medium,
        High = value.High
    };

    private static BatchRekeyReply ToGrpc(SttpMcp.Domain.Models.BatchRekeyResult value)
    {
        var updatedScopes = value.Scopes.Count(scope => scope.Applied);
        var conflictScopes = value.Scopes.Count(scope => scope.Conflict);

        var reply = new BatchRekeyReply
        {
            DryRun = value.DryRun,
            RequestedNodeIds = value.RequestedNodeIds,
            ResolvedNodeIds = value.ResolvedNodeIds,
            TemporalNodesUpdated = value.TemporalNodesUpdated,
            CalibrationsUpdated = value.CalibrationsUpdated,
            UpdatedScopes = updatedScopes,
            ConflictScopes = conflictScopes
        };

        reply.MissingNodeIds.AddRange(value.MissingNodeIds);
        reply.Scopes.AddRange(value.Scopes.Select(scope => new Grpc.ScopeRekeyResult
        {
            SourceTenantId = scope.SourceTenantId,
            SourceSessionId = DisplaySessionId(scope.SourceSessionId),
            TargetTenantId = scope.TargetTenantId,
            TargetSessionId = DisplaySessionId(scope.TargetSessionId),
            TemporalNodes = scope.TemporalNodes,
            Calibrations = scope.Calibrations,
            TargetTemporalNodes = scope.TargetTemporalNodes,
            TargetCalibrations = scope.TargetCalibrations,
            Applied = scope.Applied,
            Conflict = scope.Conflict,
            Message = scope.Message ?? string.Empty
        }));

        return reply;
    }

    private static string ResolveGrpcTenant(Metadata metadata)
    {
        foreach (var header in TenantHeaders)
        {
            var resolved = NormalizeTenantValue(metadata.GetValue(header));
            if (resolved is not null)
                return resolved;
        }

        return DefaultTenant;
    }

    private static string? NormalizeTenantValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '-' || ch == '_')
            ? normalized
            : null;
    }

    private static string ScopeSessionId(string tenant, string sessionId)
        => string.Equals(tenant, DefaultTenant, StringComparison.Ordinal)
            ? sessionId
            : $"{TenantScopePrefix}{tenant}{TenantScopeSeparator}{sessionId}";

    private static string DisplaySessionId(string sessionId)
    {
        if (!sessionId.StartsWith(TenantScopePrefix, StringComparison.Ordinal))
            return sessionId;

        var remainder = sessionId[TenantScopePrefix.Length..];
        var parts = remainder.Split(TenantScopeSeparator, 2, StringSplitOptions.None);
        return parts.Length == 2 ? parts[1] : sessionId;
    }

    private static bool SessionBelongsToTenant(string sessionId, string tenant)
    {
        if (!sessionId.StartsWith(TenantScopePrefix, StringComparison.Ordinal))
            return string.Equals(tenant, DefaultTenant, StringComparison.Ordinal);

        var remainder = sessionId[TenantScopePrefix.Length..];
        var parts = remainder.Split(TenantScopeSeparator, 2, StringSplitOptions.None);
        return parts.Length == 2 && string.Equals(parts[0], tenant, StringComparison.Ordinal);
    }

    private static SttpMcp.Domain.Models.SttpNode? NormalizeNodeForTenant(SttpMcp.Domain.Models.SttpNode node, string tenant)
        => SessionBelongsToTenant(node.SessionId, tenant)
            ? node with { SessionId = DisplaySessionId(node.SessionId) }
            : null;
}
