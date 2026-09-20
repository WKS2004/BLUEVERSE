using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Blueverse.Api.Tests.Fixtures;

public sealed class StubDestinationServer : IAsyncDisposable
{
    private readonly WebApplication _application;
    private int _disposed;

    private StubDestinationServer(WebApplication application, string address)
    {
        _application = application;
        Address = address;
    }

    public string Address { get; }

    public static async Task<StubDestinationServer> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(StubDestinationServer).Assembly.GetName().Name,
            EnvironmentName = "Testing"
        });

        builder.WebHost
            .UseKestrel()
            .UseUrls("http://127.0.0.1:0");

        var application = builder.Build();
        application.Run(async context =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync(context.RequestAborted);

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                method = context.Request.Method,
                path = context.Request.Path.Value,
                query = context.Request.QueryString.Value,
                body,
                correlationId = context.Request.Headers["X-Correlation-ID"].ToString()
            }, context.RequestAborted);
        });

        await application.StartAsync();

        var server = application.Services.GetRequiredService<IServer>();
        var address = server.Features
            .Get<IServerAddressesFeature>()
            ?.Addresses
            .SingleOrDefault();

        if (string.IsNullOrWhiteSpace(address))
        {
            await application.DisposeAsync();
            throw new InvalidOperationException("The stub destination did not expose a listening address.");
        }

        return new StubDestinationServer(application, address.TrimEnd('/') + "/");
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _application.StopAsync();
        await _application.DisposeAsync();
    }
}
