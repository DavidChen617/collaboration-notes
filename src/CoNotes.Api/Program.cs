using CoNotes.Api.BackgroundJobs;
using CoNotes.Api.Hubs;
using CoNotes.Application;
using CoNotes.Application.Abstractions;
using CoNotes.Application.AppUsers.Commands.Upsert;
using CoNotes.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddHttpContextAccessor()
    .AddEndpoints()
    .AddApiVersionConfiguration(builder.Environment.ApplicationName)
    .AddCustomResultErrorTypeMap()
    .AddProblemDetailConfiguration()
    .AddAuthenticationConfiguration(builder.Configuration, builder.Environment)
    .AddCorsConfiguration(builder.Configuration)
    .AddSignalRConfiguration(builder.Configuration, builder.Environment);

builder.AddOpenTelemetryConfiguration();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddSingleton<IChatMessageBroadcaster, ChatMessageBroadcaster>();
builder.Services.AddSingleton<AiReplyRequestQueue>();
builder.Services.AddSingleton<IAiReplyRequestQueue>(
    services => services.GetRequiredService<AiReplyRequestQueue>()
);
builder.Services.AddHostedService(
    services => services.GetRequiredService<AiReplyRequestQueue>()
);

var app = builder.Build();

app.MapOpenApi().WithDocumentPerVersion();

app.UseCors(CorsConfiguration.PolicyName);
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
app.MapHub<NoteCollabHub>("/hubs/notes");
app.MapHub<ChatHub>("/hubs/chat");

app.Run();
