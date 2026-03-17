using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AdaptiveCodecContextEngine.Diagnostics;

public class AdaptiveContextInstrumentation : IDisposable
{
    public const string ActivitySourceName = "AdaptiveCodecContext.Engine";
    public const string MeterName = "AdaptiveCodecContext";

    public ActivitySource ActivitySource { get; }
    public Meter Meter { get; }
    public Histogram<int> ComplexityHistogram { get; }

    // public double CurrentStability { get; set; }
    // public double CurrentFriction { get; set; }
    // public double CurrentAutonomy { get; set; }
    // public double CurrentLogic { get; set; }

    // public ObservableGauge<double> StabilityGauge { get; }

    public AdaptiveContextInstrumentation()
    {
        var assemblyName = typeof(AdaptiveContextInstrumentation).Assembly.GetName();
        var version = assemblyName.Version?.ToString() ?? "0.1.0";

        ActivitySource = new ActivitySource(ActivitySourceName, version);
        Meter = new Meter(MeterName, version);
        ComplexityHistogram = Meter.CreateHistogram<int>(
            "codebase.complexity",
            "1",
            "Cyclomatic complexity per function"
        );

        // Meter.CreateObservableGauge<double>(
        //     "codebase.stability",
        //     () =>
        //         new Measurement<double>(
        //             CurrentStability,
        //             new KeyValuePair<string, object?>("engine.mode", "continuous")
        //         ),
        //     unit: "1",
        //     description: "The calculated stability score (0.0 to 1.0)"
        // );

        // Meter.CreateObservableGauge<double>(
        //     "codebase.friction",
        //     () =>
        //         new Measurement<double>(
        //             CurrentFriction,
        //             new KeyValuePair<string, object?>("engine.mode", "continuous")
        //         ),
        //     description: "The calculated friction score (0.0 to 1.0)"
        // );
        //    Meter.CreateObservableGauge<double>(
        //     "codebase.stability",
        //     () =>
        //         new Measurement<double>(
        //             CurrentAutonomy,
        //             new KeyValuePair<string, object?>("engine.mode", "continuous")
        //         ),
        //     unit: "1",
        //     description: "The calculated autonomy score (0.0 to 1.0)"
        // );

        // Meter.CreateObservableGauge<double>(
        //     "codebase.friction",
        //     () =>
        //         new Measurement<double>(
        //             CurrentLogic,
        //             new KeyValuePair<string, object?>("engine.mode", "continuous")
        //         ),
        //     description: "The calculated friction score (0.0 to 1.0)"
        //);
    }

    public void Dispose()
    {
        ActivitySource.Dispose();
        Meter.Dispose();
    }
}
