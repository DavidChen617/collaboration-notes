var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddEndpoints()
    .AddHttpContextAccessor()
    .AddApiVersionConfiguration(builder.Environment.ApplicationName)
    .AddProblemDetailConfiguration()
    .AddCustomResultErrorTypeMap()
    .AddAuthenticationConfiguration();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapOpenApi()
   .WithDocumentPerVersion();

app.UseAuthorization();

app.MapEndpoints();

app.Run();

