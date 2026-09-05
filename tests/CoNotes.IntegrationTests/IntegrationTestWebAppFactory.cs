using CoNotes.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace IntegrationTests;

public sealed class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("app")
        .WithUsername("conotes_app")
        .WithPassword("password")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", _dbContainer.GetConnectionString());

        // Query handlers resolve the current user via IUserContext, which normally reads the
        // JWT-authenticated HttpContext. These tests dispatch queries directly through ISender
        // without an HTTP request, so swap in a test double whose AppUserId can be set per test.
        builder.ConfigureTestServices(services =>
        {
            services.AddScoped<TestUserContext>();
            services.AddScoped<IUserContext>(sp => sp.GetRequiredService<TestUserContext>());
        });
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        await MigrationRunner.RunAsync(_dbContainer.ExecScriptAsync);
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}
