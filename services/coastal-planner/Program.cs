using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Blueverse.CoastalPlanner.Data;
using Blueverse.CoastalPlanner.Integration;
using Blueverse.CoastalPlanner.Services;

// A shell-free Docker readiness probe for the selected minimal DHI runtime.
if (args.Contains("--healthcheck", StringComparer.Ordinal))
{
    using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    var port = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") ?? "8080";
    try { using var response = await probe.GetAsync($"http://127.0.0.1:{port}/api/planner/health"); Environment.Exit(response.IsSuccessStatusCode ? 0 : 1); }
    catch (HttpRequestException) { Environment.Exit(1); }
    catch (OperationCanceledException) { Environment.Exit(1); }
    return;
}

var builder = WebApplication.CreateBuilder(args);

// The Windows EventLog provider can throw when the application source has not
// been registered. Console/container logging remains the authoritative sink.
if (OperatingSystem.IsWindows())
{
    builder.Logging.AddFilter<Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider>(_ => false);
}

// Controllers & OpenAPI / Swagger
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BLUEVERSE Coastal Planner API",
        Version = "v1",
        Description = "Coastal itinerary management, smart recommendations and planning coordination endpoints exposed through the BLUEVERSE API gateway."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a JWT token. Swagger sends it as: Authorization: Bearer {token}"
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured.");
}

builder.Services.AddDbContext<CoastalPlannerDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null)));

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<PeerRequestContextHandler>();
builder.Services.AddHttpClient<IPeerServicesClient, PeerServicesClient>().AddHttpMessageHandler<PeerRequestContextHandler>();
builder.Services.AddHttpClient<IPlannerCatalogueClient, PlannerCatalogueClient>().AddHttpMessageHandler<PeerRequestContextHandler>();
builder.Services.AddHttpClient<IPlanningCoordinationClient, PlanningCoordinationClient>();
builder.Services.AddScoped<ICoastalPlannerService, CoastalPlannerService>();

// Security & JWT Authentication
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
        OnMessageReceived = context =>
        {
            if (string.IsNullOrWhiteSpace(context.Token))
            {
                context.Token = context.Request.Cookies["blueverse_access_token"];
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("planner.recommendations.create", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("permission", "planner.recommendations.create"));
    options.AddPolicy("planner.recommendations.read", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("permission", "planner.recommendations.read"));
    options.AddPolicy("planner.workflows.read", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("permission", "planner.workflows.read"));
    options.AddPolicy("planner.itineraries.manage", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("permission", "planner.itineraries.manage"));
    options.AddPolicy("planner.biodiversity.read", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("permission", "planner.biodiversity.read"));
});

var app = builder.Build();

// Apply database migrations / ensure schema when the database is available.
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<CoastalPlannerDbContext>();
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

        logger.LogInformation("Coastal Planner database schema is ready.");
    }
    catch (OperationCanceledException) when (initializationTimeout.IsCancellationRequested)
    {
        logger.LogWarning("Coastal Planner database initialization timed out; the service will remain unavailable until the database is reachable.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Could not initialize the coastal planner database on startup.");
    }
}

// Global Exception Handler (RFC 7807 ProblemDetails)
app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError("Unhandled Coastal Planner request failure for {Method} {Path}",
            context.Request.Method,
            context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(
            new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "An internal error occurred",
                status = StatusCodes.Status500InternalServerError,
                detail = "An unexpected error occurred processing your request."
            },
            options: null,
            contentType: "application/problem+json");
    });
});

app.UseSwagger(options =>
{
    options.RouteTemplate = "api/planner/swagger/{documentName}/swagger.json";
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Make the implicit Program class public so test projects can access it
public partial class Program { }
