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
        var defaults = new Dictionary<string, string?>
        {
            ["JWT_SIGNING_KEY"] = "api-test-only-signing-key-with-at-least-32-bytes",
            // Public API tests use a synthetic key for the private actor
            // context transform. Never read production secrets into tests.
            ["COASTAL_OPERATIONS_CONTEXT_KEY"] = Convert.ToBase64String(
                Enumerable.Range(0, 32).Select(value => (byte)value).ToArray())
        };

        if (configurationOverrides is not null)
        {
            foreach (var pair in configurationOverrides)
            {
                defaults[pair.Key] = pair.Value;
            }
        }

        _configurationOverrides = defaults;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("JWT_SIGNING_KEY", _configurationOverrides["JWT_SIGNING_KEY"]!);
        builder.UseSetting("COASTAL_OPERATIONS_CONTEXT_KEY", _configurationOverrides["COASTAL_OPERATIONS_CONTEXT_KEY"]!);
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
