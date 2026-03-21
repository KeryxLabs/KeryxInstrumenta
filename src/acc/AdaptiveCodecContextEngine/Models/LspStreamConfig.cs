namespace AdaptiveCodecContextEngine.Models;

public class LspStreamConfig
{
    public string Language { get; set; } = null!;
    public string Type { get; set; } = "stdin"; // stdin, pipe, tcp
    public string? Path { get; set; } // For named pipes
    public int? Port { get; set; } // For TCP
}
