using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Blueverse.Api.Tests.Fixtures;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly IReadOnlyDictionary<string, string?> _configurationOverrides;

    public ApiWebApplicationFactory()
        : this(null)
    {
    }

    internal ApiWebApplicationFactory(
        IReadOnlyDictionary<string, string?>? configurationOverrides = null)
    {
        _configurationOverrides = configurationOverrides
            ?? new Dictionary<string, string?>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(_configurationOverrides);
        });
        builder.ConfigureServices(services =>
        {
            services
                .AddControllers()
                .AddApplicationPart(typeof(TestOnlyErrorController).Assembly);
        });
    }
}
