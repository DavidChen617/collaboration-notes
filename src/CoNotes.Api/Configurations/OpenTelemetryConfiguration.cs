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
                            // gRPC(SDK 預設值)需要 HTTP/2-over-plaintext 的 handshake, 但 .NET
                            // 的 client 在面對非 TLS 的 collector 時會拒絕這個 handshake; HTTP/protobuf
                            // 只是單純的 HTTP POST, 沒有這個限制, 所以對這個 API 一律以非 TLS 方式
                            // 跟 SigNoz 溝通的內部流量來說是可靠的選擇(edge TLS 是在
                            // Cloudflare/nginx 終止的, 不是由 collector 本身處理)。
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
