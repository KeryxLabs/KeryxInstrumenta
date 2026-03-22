using System.ComponentModel;
using Cocona;

namespace AccCli;

/// <summary>
/// Global options available on every command.
///   acccli --host 10.0.0.5 --port 9999 stats
///   acccli --host 10.0.0.5 risk friction
/// </summary>
public record GlobalOptions(
    [Option('H', Description = "ACC engine host (overrides appsettings.json)")]
    string? Host,
    [Option('P', Description = "ACC engine port (overrides appsettings.json)")]
    int? Port
) : ICommandParameterSet;
