using System.Diagnostics;
using System.Net.Sockets;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Blueverse.CoastalPlanner.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BiodiversityFakeServiceCollection : ICollectionFixture<BiodiversityFakeServiceFixture>
{
    public const string Name = "IT3091 fake service";
}

public sealed class BiodiversityFakeServiceFixture : IAsyncLifetime
{
    private const string FakeApiKey = "fixture-only-biodiversity-key";
    private Process? _process;

    public string BaseUrl { get; private set; } = string.Empty;
    public HttpClient AdminClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var root = FindRepositoryRoot();
        var fakePath = Path.Combine(root, "services", "coastal-planner", "fake-it3091", "server.mjs");
        var port = GetAvailablePort();
        BaseUrl = $"http://127.0.0.1:{port}";
        var start = new ProcessStartInfo("node", fakePath)
        {
            WorkingDirectory = Path.GetDirectoryName(fakePath)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.Environment["PORT"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        start.Environment["IT3091_FAKE_API_KEY"] = FakeApiKey;
        _process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the IT3091 fake service.");
        AdminClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!timeout.IsCancellationRequested)
        {
            try
            {
                using var response = await AdminClient.GetAsync("/health", timeout.Token);
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            await Task.Delay(100, timeout.Token);
        }

        throw new InvalidOperationException("The IT3091 fake service did not become healthy.");
    }

    public async Task SetBehaviorAsync(string behavior)
    {
        using var response = await AdminClient.PostAsJsonAsync("/__fake/behavior", new { behavior });
        response.EnsureSuccessStatusCode();
    }

    public async Task<System.Text.Json.JsonElement> GetStateAsync()
    {
        using var response = await AdminClient.GetAsync("/__fake/state");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).Clone();
    }

    public Task DisposeAsync()
    {
        AdminClient.Dispose();
        if (_process is { HasExited: false }) _process.Kill(entireProcessTree: true);
        _process?.Dispose();
        return Task.CompletedTask;
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "services", "coastal-planner", "fake-it3091", "server.mjs")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate the BLUEVERSE repository root for the fake service.");
    }
}
