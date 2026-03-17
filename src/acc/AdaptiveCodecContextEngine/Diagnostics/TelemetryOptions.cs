namespace AdaptiveCodecContextEngine.Diagnostics
{
    public class TelemetryOptions
    {
        // Toggle telemetry on/off (opt-in)
        public bool Enabled { get; set; } = false;

        // OTLP endpoint (e.g. "http://otel-collector:4317" or "https://example.com:4317")
        public string? Endpoint { get; set; }
    }
}
