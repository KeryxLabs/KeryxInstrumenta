using System.Diagnostics;
using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Lizard;


public class LizardAnalyzer
{
    private readonly string _lizardPath;
    
    public LizardAnalyzer(string? lizardPath = null)
    {
        // Default to 'lizard' in PATH, or provide custom path
        _lizardPath = lizardPath ?? "lizard";
    }
    
    public async Task<LizardResult?> AnalyzeFileAsync(string filePath, CancellationToken ct = default)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = _lizardPath,
            Arguments = $"--json \"{filePath}\"",
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
                Console.WriteLine($"Lizard error: {error}");
                return null;
            }
            
            return ParseLizardOutput(output);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to run lizard: {ex.Message}");
            return null;
        }
    }
    
    private LizardResult? ParseLizardOutput(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<LizardResult>(json, ACCJsonContext.Default.LizardResult);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse lizard output: {ex.Message}");
            return null;
        }
    }
}

// Lizard output models

