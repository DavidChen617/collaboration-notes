using Asp.Versioning;
using CoNotes.Application;
using CoNotes.Application.AppUsers.Commands.Upsert;
using CoNotes.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpoints();

builder.Services
    .AddApiVersioning(o =>
    {
        o.AssumeDefaultVersionWhenUnspecified = true;
        o.ReportApiVersions = true;
        o.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(o =>
    {
        o.GroupNameFormat = "'v'VVV";
        o.SubstituteApiVersionInUrl = true;
    })
    .AddOpenApi();

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

builder.Services.AddCustomResultErrorTypeMap();

builder.Services.AddAuthorization();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.Audience = builder.Configuration["Authentication:Audience"];
        options.MapInboundClaims = false;
        // Local dev commonly runs Keycloak over plain HTTP; production Authority
        // is HTTPS (behind nginx/Cloudflare), so only relax this in Development.
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().WithDocumentPerVersion();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok())
   .AllowAnonymous();

app.MapPost("/api/test/app-user", async (ISender sender, HttpContext httpContext, CancellationToken ct) =>
{
    var keycloakSub = httpContext.User.FindFirst("sub")?.Value;
    if (keycloakSub is null)
        return Results.Unauthorized();

    var result = await sender.SendAsync(new UpsertAppUserCommand(keycloakSub), ct);

    return result.ToOk();
})
.RequireAuthorization();

app.MapEndpoints();

app.Run();

public partial class Program;
