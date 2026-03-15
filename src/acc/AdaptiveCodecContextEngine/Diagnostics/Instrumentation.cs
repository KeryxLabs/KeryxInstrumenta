using System.Diagnostics;
using System.Diagnostics.Metrics;


namespace AdaptiveCodecContextEngine.Diagnostics;

public class AdaptiveContextInstrumentation : IDisposable
{
    public const string ActivitySourceName = "AdaptiveCodecContext.Engine";
    public const string MeterName = "AdaptiveCodecContext";

    public ActivitySource ActivitySource { get; }
    public Meter Meter { get; }

    public AdaptiveContextInstrumentation()
    {
        var assemblyName = typeof(AdaptiveContextInstrumentation).Assembly.GetName();
        var version = assemblyName.Version?.ToString() ?? "0.1.0";

        ActivitySource = new ActivitySource(ActivitySourceName, version);
        Meter = new Meter(MeterName, version);
    }

    public void Dispose()
    {
        ActivitySource.Dispose();
        Meter.Dispose();
    }
}
