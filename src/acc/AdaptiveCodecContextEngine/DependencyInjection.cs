using AdaptiveCodecContextEngine.Diagnostics;
using OpenTelemetry.Trace;

public static class TracerProviderBuilderExtensions
{
    public static TracerProviderBuilder AddAdaptiveCodecContextInstrumentation(this TracerProviderBuilder builder)
    {
        return builder.AddSource(AdaptiveContextInstrumentation.ActivitySourceName);
    }
}
