using Microsoft.Extensions.Logging;

namespace AdaptiveCodecContextEngine.Diagnostics;

public static partial class Logger
{
    [LoggerMessage(
        EventId = (int)Event.DatabaseWrite,
        Level = LogLevel.Error,
        Message = "Database Write Error for {SurrealDbError}"
    )]
    public static partial void LogDbWriteError(this ILogger logger, string? surrealDbError);

    [LoggerMessage(
        EventId = (int)Event.DatabaseRead,
        Level = LogLevel.Error,
        Message = "Database Read Error for {SurrealDbError}"
    )]
    public static partial void LogDbReadError(this ILogger logger, string? surrealDbError);

    [LoggerMessage(
        EventId = (int)Event.AvecAnalysis,
        Level = LogLevel.Debug,
        Message = "AVEC OUTPUT for {Name}: S={Stability:F2}, L={Logic:F2}, F={Friction:F2}, A={Autonomy:F2}"
    )]
    public static partial void LogAvecCalculation(
        this ILogger logger,
        string name,
        double stability,
        double logic,
        double friction,
        double autonomy
    );

    [LoggerMessage(
        EventId = (int)Event.LspRead,
        Level = LogLevel.Debug,
        Message = "Received LSP message: method={Method}, hasResult={HasResult}, language={Language}"
    )]
    public static partial void LogLspReceipt(
        this ILogger logger,
        string? method,
        bool hasResult,
        string language
    );

    // 2. Friction Pillar
    [LoggerMessage(
        EventId = (int)Event.AvecAnalysis,
        Level = LogLevel.Error,
        Message = "Developer friction detected during {Stage}: {FrictionType}"
    )]
    public static partial void LogFriction(ILogger logger, string stage, string frictionType);

    // 3. Logic/Autonomy Pillars
    [LoggerMessage(
        EventId = (int)Event.AvecAnalysis,
        Level = LogLevel.Information,
        Message = "Autonomy transition: {PreviousState} -> {NewState}. Logic Hash: {LogicHash}"
    )]
    public static partial void LogAutonomyChange(
        ILogger logger,
        string previousState,
        string newState,
        string logicHash
    );
}

public enum Event
{
    DatabaseRead = 10,
    DatabaseWrite = 11,
    AvecAnalysis = 12,
    LspRead = 13,
    QueryRequested = 14,
    GitChange = 15,
    CodeAnalysis = 16,
}

public readonly record struct SurrealDbErrorDetails(string Code, string Details);
