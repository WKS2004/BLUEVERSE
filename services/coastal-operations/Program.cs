using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// The Windows EventLog provider can throw when the application source has not
// been registered. Console/container logging remains the authoritative sink.
if (OperatingSystem.IsWindows())
{
    builder.Logging.AddFilter<Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider>(_ => false);
}

builder.Services.AddControllers();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BLUEVERSE Coastal Operations API",
        Version = "v1",
        Description = "Private Coastal Operations service contract exposed through the BLUEVERSE public API gateway."
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

builder.Services.AddSingleton(new CoastalActorContextEnvelopeVerifier(builder.Configuration));
builder.Services.AddSingleton<CoastalActorContextReplayGuard>();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CoastalActorContextAuthenticationHandler.SchemeName;
    options.DefaultChallengeScheme = CoastalActorContextAuthenticationHandler.SchemeName;
})
.AddScheme<AuthenticationSchemeOptions, CoastalActorContextAuthenticationHandler>(
    CoastalActorContextAuthenticationHandler.SchemeName,
    _ => { });

builder.Services.AddAuthorization(CoastalAuthorizationPolicies.Configure);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured.");
}

builder.Services.AddDbContext<CoastalOperationsDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null)));

builder.Services.Configure<ComponentDependencyOptions>(builder.Configuration.GetSection("ComponentDependencies"));
builder.Services.AddHttpClient(ComponentDependencyCollector.HttpClientName, client =>
    client.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddSingleton<ComponentDependencyHealthRegistry>();
builder.Services.AddScoped<IComponentDependencyCollector, ComponentDependencyCollector>();
builder.Services.AddScoped<IdempotencyStore>();
builder.Services.AddScoped<AssessmentApplicationService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<OperationsAuditReader>();
builder.Services.AddSingleton<ICoastalReferencePort, DisconnectedCoastalReferencePort>();
builder.Services.AddScoped<AlertApplicationService>();
builder.Services.AddScoped<TargetStatusApplicationService>();
builder.Services.Configure<EvidenceStorageOptions>(builder.Configuration.GetSection("EvidenceStorage"));
builder.Services.AddSingleton<IAssessmentEvidenceStorage, FileSystemAssessmentEvidenceStorage>();
builder.Services.AddSingleton<AssessmentEvidenceSanitizer>();
builder.Services.AddScoped<AssessmentEvidenceApplicationService>();
builder.Services.AddSingleton<IAssessmentProposalPort, DisconnectedAssessmentProposalPort>();
builder.Services.AddScoped<AssessmentDispatchDelivery>();
builder.Services.AddHostedService<AssessmentDispatchHostedService>();
builder.Services.AddHostedService<AlertExpirationHostedService>();
builder.Services.AddHostedService<AssessmentEvidenceRetentionHostedService>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (CoastalOperationsException exception)
    {
        if (context.Response.HasStarted) throw;
        context.Response.Clear();
        context.Response.StatusCode = exception.StatusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Type = "about:blank",
            Title = exception.Title,
            Status = exception.StatusCode,
            Detail = exception.Message,
            Instance = context.Request.Path,
            Extensions = { ["code"] = exception.Code }
        }, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
    catch (Exception exception)
    {
        if (context.Response.HasStarted) throw;
        app.Logger.LogError(exception, "Unhandled Coastal Operations request failure.");
        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Type = "about:blank",
            Title = "Coastal Operations error",
            Status = StatusCodes.Status500InternalServerError,
            Detail = "An unexpected error occurred.",
            Instance = context.Request.Path
        }, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
});

app.UseStatusCodePages(async (StatusCodeContext statusCodeContext) =>
{
    var response = statusCodeContext.HttpContext.Response;
    var status = response.StatusCode;
    response.ContentType = "application/problem+json";
    var title = status switch
    {
        StatusCodes.Status401Unauthorized => "Authentication is required",
        StatusCodes.Status403Forbidden => "Permission denied",
        StatusCodes.Status404NotFound => "Resource not found",
        _ => "Request failed"
    };
    await response.WriteAsJsonAsync(new ProblemDetails
    {
        Type = "about:blank",
        Title = title,
        Status = status,
        Instance = statusCodeContext.HttpContext.Request.Path
    }, options: null, contentType: "application/problem+json", cancellationToken: statusCodeContext.HttpContext.RequestAborted);
});

app.UseSwagger(options =>
{
    options.RouteTemplate = "api/operations/swagger/{documentName}/swagger.json";
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await InitializeDatabaseAsync(app);
app.Run();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<CoastalOperationsDbContext>();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    try
    {
        await db.Database.MigrateAsync(timeout.Token);
        logger.LogInformation("Coastal Operations database schema is ready.");
    }
    catch (OperationCanceledException) when (timeout.IsCancellationRequested)
    {
        logger.LogWarning("Coastal Operations database initialization timed out; readiness will remain unhealthy until migration succeeds.");
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Could not initialize the Coastal Operations database on startup.");
    }
}

public partial class Program { }
