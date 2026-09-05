using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CoNotes.Api.Configurations;

internal static class OpenTelemetryConfiguration
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddOpenTelemetryConfiguration()
        {
            var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];

            builder.Services
                .AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
                .WithTracing(tracing =>
                {
                    tracing.AddAspNetCoreInstrumentation();

                    if (otlpEndpoint is not null)
                        tracing.AddOtlpExporter(o =>
                        {
                            // gRPC (the SDK's default) needs an HTTP/2-over-plaintext handshake that
                            // .NET's client refuses against a non-TLS collector; HTTP/protobuf is a
                            // plain HTTP POST and has no such requirement, so it's the reliable choice
                            // for the internal, non-TLS traffic this API always talks to SigNoz over
                            // (edge TLS is terminated at Cloudflare/nginx, not by the collector itself).
                            o.Protocol = OtlpExportProtocol.HttpProtobuf;
                            o.Endpoint = new Uri($"{otlpEndpoint}/v1/traces");
                        });
                });

            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;

                if (otlpEndpoint is not null)
                    logging.AddOtlpExporter(o =>
                    {
                        o.Protocol = OtlpExportProtocol.HttpProtobuf;
                        o.Endpoint = new Uri($"{otlpEndpoint}/v1/logs");
                    });
            });

            return builder;
        }
    }
}
