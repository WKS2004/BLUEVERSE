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
        Description = "Use Authorization: Bearer {token}. Signed-in browser requests may also use the Auth-selected account session cookie."
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

// JWT validation uses the shared issuer/audience/signing key contract. Auth
// session state is checked as well so revoked or stale tokens cannot reach
// marine operations through the private service route.
var secretKey = builder.Configuration["JWT_SIGNING_KEY"];
if (string.IsNullOrWhiteSpace(secretKey))
{
    throw new InvalidOperationException("JWT_SIGNING_KEY must be configured.");
}

var secretKeyBytes = Encoding.UTF8.GetBytes(secretKey);
if (secretKeyBytes.Length < 32)
{
    throw new InvalidOperationException("JWT_SIGNING_KEY must be at least 32 UTF-8 bytes.");
}

var issuer = builder.Configuration["Jwt:Issuer"] ?? "Blueverse.Auth";
var audience = builder.Configuration["Jwt:Audience"] ?? "Blueverse.Client";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(secretKeyBytes),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = MarineSafetyCookieTokenReader.OnMessageReceived,
        OnTokenValidated = async context =>
        {
            var tokenValidator = context.HttpContext.RequestServices.GetRequiredService<IIdentityTokenValidator>();

            var principal = context.Principal;
            var subject = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (!Guid.TryParse(subject, out var userId) ||
                !int.TryParse(principal?.FindFirstValue("token_version"), out var tokenVersion) ||
                !Guid.TryParse(principal?.FindFirstValue("session_id"), out var sessionId) ||
                !int.TryParse(principal?.FindFirstValue("session_version"), out var sessionVersion))
            {
                context.Fail("The token identity or session claims are invalid.");
                return;
            }

            var isCurrent = await tokenValidator.IsCurrentAsync(
                userId,
                tokenVersion,
                sessionId,
                sessionVersion,
                context.HttpContext.RequestAborted);
            if (!isCurrent)
            {
                context.Fail("The account or device session is no longer active.");
            }
        }
    };
});

builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPermissionResolver, IdentityPermissionResolver>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddScoped<IIdentityTokenValidator, IdentityTokenValidator>();

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
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc7807",
            title = "Marine Safety Service Error",
            status = StatusCodes.Status500InternalServerError,
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
