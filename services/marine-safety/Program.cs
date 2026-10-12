using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Blueverse.MarineSafety.Authorization;
using Blueverse.MarineSafety.Data;
using Blueverse.MarineSafety.Providers;
using Blueverse.MarineSafety.Services;
using Blueverse.MarineSafety.Dtos;

if (args.Contains("--healthcheck", StringComparer.Ordinal))
{
    var port = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") ?? "8080";
    using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    try
    {
        using var response = await probe.GetAsync($"http://127.0.0.1:{port}/api/marine/health", HttpCompletionOption.ResponseHeadersRead);
        Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException) { Environment.ExitCode = 1; }
    catch (OperationCanceledException) { Environment.ExitCode = 1; }
    return;
}

var builder = WebApplication.CreateBuilder(args);

// The Windows EventLog provider can throw when the application source has not
// been registered. Console/container logging remains the authoritative sink.
if (OperatingSystem.IsWindows())
{
    builder.Logging.AddFilter<Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider>(_ => false);
}

// Controllers & OpenAPI
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BLUEVERSE Marine Conditions & Safety API",
        Version = "v1",
        Description = "Internal marine-safety component service: condition snapshots, safety profiles and deterministic suitability (exposed to clients only through the public API gateway)."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Use an Auth bearer token at the public API gateway. The private service receives only the verified gateway actor context."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

// Database: shared PostgreSQL, marine-safety-owned domain tables.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured.");
}

builder.Services.AddDbContext<MarineSafetyDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null)));

// The private service accepts only request-bound actor contexts issued by the public API.
var actorKey = Blueverse.Contracts.MarineActorContext.ReadKey(builder.Configuration[Blueverse.Contracts.MarineActorContext.KeySetting]);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = Blueverse.Contracts.MarineActorContext.Validation(actorKey);
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // No bearer or cookie fallback at the private service boundary.
            var values = context.Request.Headers[Blueverse.Contracts.MarineActorContext.Header];
            if (values.Count != 1 || string.IsNullOrWhiteSpace(values[0])) context.NoResult();
            else context.Token = values[0];
            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            if (context.Principal is null || !await Blueverse.Contracts.MarineActorContext.MatchesAsync(
                context.Principal, context.Request, context.HttpContext.RequestAborted))
                context.Fail("Invalid gateway operation context.");
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPermissionResolver, GatewayPermissionResolver>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddHttpClient<IExperienceCatalogueClient, ExperienceCatalogueClient>(client =>
{
    var configured = builder.Configuration["EXPERIENCE_CATALOGUE_URL"] ?? "http://experience-biodiversity:8080";
    if (Uri.TryCreate(configured.TrimEnd('/') + "/", UriKind.Absolute, out var address) &&
        address.Scheme is "http" or "https" && string.IsNullOrEmpty(address.UserInfo) &&
        string.IsNullOrEmpty(address.Query) && string.IsNullOrEmpty(address.Fragment))
        client.BaseAddress = address;
    client.Timeout = TimeSpan.FromSeconds(5);
});

// Provider configuration and typed HTTP clients for Open-Meteo.
builder.Services.Configure<OpenMeteoOptions>(builder.Configuration.GetSection(OpenMeteoOptions.SectionName));
builder.Services.AddHttpClient("OpenMeteoWeather", client =>
{
    var options = builder.Configuration
        .GetSection(OpenMeteoOptions.SectionName)
        .Get<OpenMeteoOptions>() ?? new OpenMeteoOptions();
    client.BaseAddress = new Uri(options.WeatherBaseUrl);
});
builder.Services.AddHttpClient("OpenMeteoMarine", client =>
{
    var options = builder.Configuration
        .GetSection(OpenMeteoOptions.SectionName)
        .Get<OpenMeteoOptions>() ?? new OpenMeteoOptions();
    client.BaseAddress = new Uri(options.MarineBaseUrl);
});
builder.Services.AddSingleton<IFreshnessPolicy, FreshnessPolicy>();
builder.Services.AddSingleton<IOpenMeteoClient, OpenMeteoClient>();

// Application services.
builder.Services.AddScoped<IConditionService, ConditionService>();
builder.Services.AddScoped<ISuitabilityService, SuitabilityService>();
builder.Services.AddScoped<ISafetyProfileService, SafetyProfileService>();
builder.Services.AddScoped<IActivityService, ActivityService>();

// Forwarded Headers for Edge-Nginx reverse proxy.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Apply marine-safety migrations when the database is available. Domain
// migrations stay in this owning service; identity migrations stay in Auth.
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<MarineSafetyDbContext>();
    using var initializationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    try
    {
        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync(initializationTimeout.Token);
        }
        else
        {
            await db.Database.EnsureCreatedAsync(initializationTimeout.Token);
        }

        logger.LogInformation("Marine-safety database schema is ready.");
    }
    catch (OperationCanceledException) when (initializationTimeout.IsCancellationRequested)
    {
        logger.LogCritical("Marine-safety database initialization timed out; refusing to start without the required schema.");
        throw;
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Could not initialize the marine-safety database; refusing to start without the required schema.");
        throw;
    }
}

// Global Exception Handler (RFC 7807 ProblemDetails), matching the gateway.
// In the Testing environment the original exception message is included so
// integration tests fail with actionable evidence instead of opaque 500s.
app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        var catalogueUnavailable = exception is CatalogueUnavailableException;
        context.Response.StatusCode = catalogueUnavailable ? 503 : 500;
        if (catalogueUnavailable) context.Response.Headers.RetryAfter = "5";
        context.Response.ContentType = "application/problem+json";
        if (exception is CatalogueUnavailableException unavailable)
        {
            await context.Response.WriteAsJsonAsync(DependencyUnavailableProblemDetails.Catalogue(unavailable.Reason),
                options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
            return;
        }
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc7807",
            title = "Marine Safety Service Error",
            status = context.Response.StatusCode,
            detail = exception is null || !app.Environment.IsEnvironment("Testing")
                ? "An unexpected error occurred processing your request."
                : exception.Message
        }, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    });
});

app.UseForwardedHeaders();
app.UseSwagger(options =>
{
    options.RouteTemplate = "api/marine/swagger/{documentName}/swagger.json";
});
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "api/marine/swagger";
    options.SwaggerEndpoint("/api/marine/swagger/v1/swagger.json", "BLUEVERSE Marine Conditions & Safety API");
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Make the implicit Program class public so test projects can access it.
public partial class Program { }
