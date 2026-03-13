using System.Diagnostics;
using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Lizard;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

public class LizardAnalyzer
{
    private readonly string _lizardPath;
    private readonly ILogger<LizardAnalyzer> _logger;

    public LizardAnalyzer(ILogger<LizardAnalyzer> logger)
    {
        _lizardPath = "lizard";
        _logger = logger;
    }

    public async Task<LizardResult?> AnalyzeFileAsync(string filePath, CancellationToken ct = default)
    {
        //_logger.LogInformation("Analyzing {file}", filePath);
        var processStartInfo = new ProcessStartInfo
        {
            FileName = _lizardPath,
            Arguments = $"--csv \"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processStartInfo };

        try
        {
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var error = await process.StandardError.ReadToEndAsync(ct);
            
            await process.WaitForExitAsync(ct);
            
            if (process.ExitCode != 0)
            {
                _logger.LogError("Lizard exited with code {ExitCode}: {Error}", process.ExitCode, error);
                return null;
            }

            return ParseLizardCsv(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run lizard on {FilePath}", filePath);
            return null;
        }
    }

    private LizardResult? ParseLizardCsv(string csv)
    {
        try
        {
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 1) 
                return null;

            var nllocIndex = (int)Headers.NLOC;
            var ccnIndex = (int)Headers.CCN;
            var tokenIndex = (int)Headers.Token;
            var paramIndex = (int)Headers.Parameter;
            var nameIndex = (int)Headers.Name;
            var startIndex = (int)Headers.StartLine;
            var endIndex = (int)Headers.EndLine;
            var longNameIndex = (int)Headers.LongName;

            var functions = new List<LizardFunction>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var columns = ParseCsvLine(line);

                if (columns.Length <= Math.Max(Math.Max(nllocIndex, ccnIndex), nameIndex))
                    continue;

                var function = new LizardFunction
                {
                    Name = GetColumn(columns, nameIndex) ?? "unknown",
                    LongName = GetColumn(columns, longNameIndex) ?? GetColumn(columns, nameIndex) ?? "unknown",
                    Nloc = ParseInt(GetColumn(columns, nllocIndex)),
                    CyclomaticComplexity = ParseInt(GetColumn(columns, ccnIndex)),
                    TokenCount = ParseInt(GetColumn(columns, tokenIndex)),
                    ParameterCount = ParseInt(GetColumn(columns, paramIndex)),
                    StartLine = ParseInt(GetColumn(columns, startIndex)),
                    EndLine = ParseInt(GetColumn(columns, endIndex))
                };

                functions.Add(function);
            }

            return new LizardResult
            {
                FunctionList = functions
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse lizard CSV output.");
            return null;
        }
    }

    private string[] ParseCsvLine(string line)
    {
        // Simple CSV parser - handles quoted fields with commas
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString().Trim());
        return result.ToArray();
    }

    private string? GetColumn(string[] columns, int index)
    {
        if (index < 0 || index >= columns.Length)
            return null;

        var value = columns[index].Trim('"').Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private int ParseInt(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }

    private enum Headers
    {
        NLOC = 0,
        CCN = 1,
        Token = 2,
        Parameter = 3,
        Length = 4,
        Location = 5,
        File = 6,
        Name = 7,
        LongName = 8,
        StartLine = 9,
        EndLine = 10
    }
}


