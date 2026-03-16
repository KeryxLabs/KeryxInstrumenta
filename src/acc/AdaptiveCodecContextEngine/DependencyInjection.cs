using AdaptiveCodecContextEngine.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

public static class TracerProviderBuilderExtensions
{
    public static TracerProviderBuilder AddAdaptiveCodecContextInstrumentation(
        this TracerProviderBuilder builder
    )
    {
        return builder.AddSource(AdaptiveContextInstrumentation.ActivitySourceName);
    }
}

public static class TelemetryRegistration
{
    public static IServiceCollection AddTelemetry(
        this IServiceCollection services,
        IConfiguration config
    )
    {
        // Bind telemetry options from configuration (Acc:Telemetry)
        var telemetrySection = config.GetSection("Acc:Telemetry");
        var telemetryOptions = telemetrySection.Get<TelemetryOptions>() ?? new TelemetryOptions();
        services.Configure<TelemetryOptions>(telemetrySection);

        var stabilityWeight = config.GetValue("Acc:Weights:Stability", 0.5);
        var logicWeight = config.GetValue("Acc:Weights:Logic", 0.5);

        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService("AdaptiveCodecContext")
            .AddAttributes(
                new Dictionary<string, object>
                {
                    ["engine.weights.stability"] = stabilityWeight,
                    ["engine.weights.logic"] = logicWeight,
                    ["engine.run_id"] = Guid.NewGuid().ToString(),
                }
            );

        services.AddSingleton<AdaptiveContextInstrumentation>();

        if (telemetryOptions.Enabled)
        {
            // Configure OpenTelemetry exporters when telemetry is enabled
            var otlpEndpoint = telemetryOptions.Endpoint;
            services
                .AddOpenTelemetry()
                .ConfigureResource(resource =>
                    resource.AddService(
                        serviceName: AdaptiveContextInstrumentation.ActivitySourceName,
                        serviceInstanceId: Environment.MachineName
                    )
                )
                .WithTracing(tracing =>
                    tracing
                        .AddSource(AdaptiveContextInstrumentation.ActivitySourceName)
                        .AddOtlpExporter(exporterOptions =>
                        {
                            exporterOptions.Endpoint = new Uri(otlpEndpoint!);
                            exporterOptions.Protocol = OpenTelemetry
                                .Exporter
                                .OtlpExportProtocol
                                .Grpc;
                        })
                )
                .WithMetrics(metrics =>
                    metrics
                        .AddMeter(AdaptiveContextInstrumentation.MeterName)
                        .AddRuntimeInstrumentation()
                        .AddOtlpExporter(
                            (exporterOptions, metricReaderOptions) =>
                            {
                                exporterOptions.Endpoint = new Uri(otlpEndpoint!);
                                exporterOptions.Protocol = OpenTelemetry
                                    .Exporter
                                    .OtlpExportProtocol
                                    .Grpc;
                                metricReaderOptions
                                    .PeriodicExportingMetricReaderOptions
                                    .ExportIntervalMilliseconds = 1000;
                            }
                        )
                )
                .WithLogging(builder =>
                {
                    builder.AddOtlpExporter(exporterOptions =>
                    {
                        exporterOptions.Endpoint = new Uri(otlpEndpoint!);
                        exporterOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    });
                });

            return services;

            // services
            //     .AddOpenTelemetry()
            //     .ConfigureResource(
            //         (rb) =>
            //         {
            //             rb = resourceBuilder;
            //         }
            //     )
            //     .WithTracing(tracing =>
            //     {
            //         tracing.AddSource(AdaptiveContextInstrumentation.ActivitySourceName);
            //         tracing.AddOtlpExporter(opts =>
            //         {
            //             if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            //                 opts.Endpoint = new Uri(otlpEndpoint);
            //             opts.ExportProcessorType = OpenTelemetry.ExportProcessorType.Batch;
            //         });
            //     })
            //     .WithMetrics(metrics =>
            //     {
            //         metrics
            //             .AddMeter(AdaptiveContextInstrumentation.MeterName)
            //             .AddRuntimeInstrumentation();
            //         metrics.AddOtlpExporter(opts =>
            //         {
            //             if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            //                 opts.Endpoint = new Uri(otlpEndpoint);
            //         });
            //     });

            // // Configure Logging to export via OTLP
            // services.AddLogging(logging =>
            // {
            //     logging.ClearProviders();
            //     logging.AddOpenTelemetry(options =>
            //     {
            //         options.SetResourceBuilder(resourceBuilder);
            //         options.IncludeFormattedMessage = true;
            //         options.IncludeScopes = true;
            //         options.AddOtlpExporter(opts =>
            //         {
            //             if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            //                 opts.Endpoint = new Uri(otlpEndpoint);
            //             opts.ExportProcessorType = OpenTelemetry.ExportProcessorType.Batch;
            //             opts.BatchExportProcessorOptions.MaxExportBatchSize = 512;
            //             opts.BatchExportProcessorOptions.ScheduledDelayMilliseconds = 5000;
            //         });
            //     });
            // });
        }
        else
        {
            // Telemetry disabled: fall back to console with error-only logging
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Error);
            });
        }

        return services;
    }
}
