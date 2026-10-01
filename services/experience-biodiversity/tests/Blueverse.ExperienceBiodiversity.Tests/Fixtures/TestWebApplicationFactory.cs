using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Blueverse.ExperienceBiodiversity.Data;

namespace Blueverse.ExperienceBiodiversity.Tests.Fixtures;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"blueverse-exp-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=testing;Database=testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=testing;Database=testing"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ExperienceBiodiversityDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<ExperienceBiodiversityDbContext>();

            var inMemoryServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<ExperienceBiodiversityDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
                options.UseInternalServiceProvider(inMemoryServiceProvider);
            });
        });
    }

    public HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Remove("X-User-Id");
        client.DefaultRequestHeaders.Remove("X-User-Roles");
        client.DefaultRequestHeaders.Remove("X-User-Permissions");
        client.DefaultRequestHeaders.Add("X-User-Id", "22222222-2222-2222-2222-222222222201");
        client.DefaultRequestHeaders.Add("X-User-Roles", "Admin");
        client.DefaultRequestHeaders.Add("X-User-Permissions", "experiences.catalogue.manage,experiences.catalogue.read,auth.role.system.manage");
        return client;
    }

    public HttpClient CreateAnonymousClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Remove("X-User-Id");
        client.DefaultRequestHeaders.Remove("X-User-Roles");
        client.DefaultRequestHeaders.Remove("X-User-Permissions");
        client.DefaultRequestHeaders.Remove("Authorization");
        return client;
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        // By default provide Admin headers so existing integration tests execute with full permissions
        client.DefaultRequestHeaders.Add("X-User-Id", "22222222-2222-2222-2222-222222222201");
        client.DefaultRequestHeaders.Add("X-User-Roles", "Admin");
        client.DefaultRequestHeaders.Add("X-User-Permissions", "experiences.catalogue.manage,experiences.catalogue.read,auth.role.system.manage");
    }
}
