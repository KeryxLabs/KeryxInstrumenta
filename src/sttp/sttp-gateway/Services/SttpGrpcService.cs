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
    MonthlyRollupService monthlyRollupService)
    : SttpGatewayService.SttpGatewayServiceBase
{
    public override async Task<CalibrateSessionReply> CalibrateSession(CalibrateSessionRequest request, ServerCallContext context)
    {
        var result = await calibrationService.CalibrateAsync(
            request.SessionId,
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
        var result = await storeContextService.StoreAsync(request.Node, request.SessionId, context.CancellationToken);

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
        var result = await contextQueryService.GetContextAsync(
            request.SessionId,
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

        reply.Nodes.AddRange(result.Nodes.Select(ToGrpc));
        return reply;
    }

    public override async Task<ListNodesReply> ListNodes(ListNodesRequest request, ServerCallContext context)
    {
        var result = await contextQueryService.ListNodesAsync(
            request.Limit <= 0 ? 50 : request.Limit,
            string.IsNullOrWhiteSpace(request.SessionId) ? null : request.SessionId,
            context.CancellationToken);

        var reply = new ListNodesReply
        {
            Retrieved = result.Retrieved
        };

        reply.Nodes.AddRange(result.Nodes.Select(ToGrpc));
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

    public override async Task<CreateMonthlyRollupReply> CreateMonthlyRollup(CreateMonthlyRollupRequest request, ServerCallContext context)
    {
        var monthlyRequest = new MonthlyRollupRequest
        {
            SessionId = request.SessionId,
            StartUtc = request.StartUtc.ToDateTime(),
            EndUtc = request.EndUtc.ToDateTime(),
            SourceSessionId = string.IsNullOrWhiteSpace(request.SourceSessionId) ? null : request.SourceSessionId,
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
            SessionId = value.SessionId,
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
}
