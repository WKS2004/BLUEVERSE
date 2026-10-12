using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Blueverse.ExperienceBiodiversity.Data;
using Blueverse.ExperienceBiodiversity.Services;

var builder = WebApplication.CreateBuilder(args);

var signingKey = builder.Configuration["JWT_SIGNING_KEY"];
if (string.IsNullOrWhiteSpace(signingKey))
{
    throw new InvalidOperationException("JWT_SIGNING_KEY must be configured.");
}

var signingKeyBytes = Encoding.UTF8.GetBytes(signingKey);
if (signingKeyBytes.Length < 32)
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
        IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
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

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// Service Registrations
builder.Services.AddScoped<IResilientHttpExecutor, ResilientHttpExecutor>();
builder.Services.AddScoped<IUserContext, UserContext>();
builder.Services.AddScoped<IOperationalStatusConsumerService, OperationalStatusConsumerService>();
builder.Services.AddScoped<IBiodiversityConsumerService, BiodiversityConsumerService>();
builder.Services.AddScoped<IMarineSafetyConsumerService, MarineSafetyConsumerService>();
builder.Services.AddScoped<IDependenciesDiagnosticsService, DependenciesDiagnosticsService>();
builder.Services.AddScoped<IMapProviderService, MapProviderService>();
builder.Services.AddScoped<IEvaluationService, EvaluationService>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured.");
}

builder.Services.AddDbContext<ExperienceBiodiversityDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions
            .MigrationsHistoryTable(
                "__EFMigrationsHistory_ExperienceBiodiversity")
            .EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null)));

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BLUEVERSE Experience & Biodiversity API",
        Version = "v1",
        Description = "Private service contract exposed through the BLUEVERSE public API gateway."
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

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<ExperienceBiodiversityDbContext>();
    using var initializationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

    try
    {
        if (!db.Database.IsRelational() ||
            await db.Database.CanConnectAsync(initializationTimeout.Token))
        {
            if (db.Database.IsRelational())
            {
                await db.Database.MigrateAsync(initializationTimeout.Token);
            }
            else
            {
                await db.Database.EnsureCreatedAsync(initializationTimeout.Token);
            }

            // Seed canonical initial catalog data
            await DataSeeder.SeedAsync(db, initializationTimeout.Token);

            logger.LogInformation("Experience & Biodiversity database schema and seed data are ready.");
        }
        else
        {
            logger.LogWarning(
                "Experience & Biodiversity database is unavailable; process liveness will continue independently.");
        }
    }
    catch (OperationCanceledException) when (initializationTimeout.IsCancellationRequested)
    {
        logger.LogWarning("Experience & Biodiversity database initialization timed out.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Could not initialize the Experience & Biodiversity database on startup.");
    }
}

app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var exFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var ex = exFeature?.Error;

        logger.LogError(
            ex,
            "Unhandled Experience & Biodiversity request failure for {Method} {Path}: {Message}",
            context.Request.Method,
            context.Request.Path,
            ex?.Message);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var isDevOrTest = app.Environment.IsDevelopment() || string.Equals(app.Environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase);

        await context.Response.WriteAsJsonAsync(
            new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "An internal error occurred",
                status = StatusCodes.Status500InternalServerError,
                detail = isDevOrTest && ex != null ? ex.ToString() : "An unexpected error occurred processing your request."
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    });
});

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger(options =>
{
    options.RouteTemplate = "api/experiences/swagger/{documentName}/swagger.json";
});

app.MapControllers();

app.Run();

public partial class Program { }
