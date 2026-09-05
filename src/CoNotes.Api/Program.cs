using CoNotes.Application;
using CoNotes.Application.AppUsers.Commands.Upsert;
using CoNotes.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddHttpContextAccessor()
    .AddEndpoints()
    .AddApiVersionConfiguration()
    .AddCustomResultErrorTypeMap()
    .AddAuthenticationConfiguration(builder.Configuration, builder.Environment);

builder.AddOpenTelemetryConfiguration();

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
