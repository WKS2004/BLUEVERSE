using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Blueverse.Auth.Authorization;
using Blueverse.Auth.Data;
using Blueverse.Auth.Services;
using Blueverse.Auth.Security;

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
        Title = "BLUEVERSE Auth API",
        Version = "v1",
        Description = "Authentication, user, role and permission endpoints exposed through the BLUEVERSE API gateway."
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

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null)));
builder.Services.Configure<AuthSessionOptions>(
    builder.Configuration.GetSection(AuthSessionOptions.SectionName));

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
                var selectedAccount = context.Request.Cookies[AuthCookieNames.ActiveAccountId];
                if (Guid.TryParse(selectedAccount, out var userId))
                {
                    context.Token = context.Request.Cookies[AuthCookieNames.AccessTokenFor(userId)];
                    var legacyToken = context.Request.Cookies[AuthCookieNames.LegacyAccessToken];
                    if (string.IsNullOrWhiteSpace(context.Token) &&
                        AuthCookieNames.ReadUserId(legacyToken) == userId)
                    {
                        context.Token = legacyToken;
                    }
                }
                else
                {
                    context.Token = context.Request.Cookies[AuthCookieNames.LegacyAccessToken];
                }
            }

            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            var subject = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? context.Principal?.FindFirst("sub")?.Value;
            var tokenVersionClaim = context.Principal?.FindFirst("token_version")?.Value;
            var sessionIdClaim = context.Principal?.FindFirst("session_id")?.Value;
            var sessionVersionClaim = context.Principal?.FindFirst("session_version")?.Value;

            if (!Guid.TryParse(subject, out var userId) ||
                !int.TryParse(tokenVersionClaim, out var tokenVersion) ||
                !Guid.TryParse(sessionIdClaim, out var sessionId) ||
                !int.TryParse(sessionVersionClaim, out var sessionVersion))
            {
                context.Fail("The token subject, token version or session claims are invalid.");
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<AuthDbContext>();
            var currentUser = await db.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new { user.IsActive, user.TokenVersion })
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
            var activeSession = await db.ActiveSessions
                .AsNoTracking()
                .AnyAsync(session =>
                    session.Id == sessionId &&
                    session.UserId == userId &&
                    session.SessionVersion == sessionVersion &&
                    session.ExpiresAt > DateTime.UtcNow,
                    context.HttpContext.RequestAborted);

            if (currentUser is null ||
                !currentUser.IsActive ||
                currentUser.TokenVersion != tokenVersion ||
                !activeSession)
            {
                context.Fail("The account, device session or token has been revoked.");
            }
        }
    };
});

// Permission Authorization
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();

// Application Services
builder.Services.AddScoped<IPasswordHasherService, PasswordHasherService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHostedService<SessionCleanupHostedService>();

var app = builder.Build();

// Apply database migrations and seed the bootstrap administrator when the database is available.
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
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

        logger.LogInformation("Authentication database schema is ready.");
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();
        await AuthDataSeeder.SeedAdminAsync(db, builder.Configuration, passwordHasher, logger, initializationTimeout.Token);
    }
    catch (OperationCanceledException) when (initializationTimeout.IsCancellationRequested)
    {
        logger.LogWarning("Authentication database initialization timed out; the service will remain unavailable until the database is reachable.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Could not initialize or seed the authentication database on startup.");
    }
}

// Global Exception Handler
app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError("Unhandled Auth request failure for {Method} {Path}",
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
    options.RouteTemplate = "api/auth/swagger/{documentName}/swagger.json";
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Make the implicit Program class public so test projects can access it
public partial class Program { }
