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

    public string ConnectionString => _dbContainer.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", _dbContainer.GetConnectionString());

        // Query handler 是透過 IUserContext 解析目前使用者, 正常情況下會讀取
        // JWT-authenticated 的 HttpContext。這些測試是直接透過 ISender 發送 query,
        // 沒有實際的 HTTP request, 所以換成一個可以逐測試設定 AppUserId 的 test double。
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
