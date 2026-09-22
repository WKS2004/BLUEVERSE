using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// The Windows EventLog provider can throw when the application source has not
// been registered. Console/container logging remains the authoritative sink.
if (OperatingSystem.IsWindows())
{
    builder.Logging.AddFilter<Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider>(_ => false);
}

// Controllers & OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BLUEVERSE Public API",
        Version = "v1",
        Description = "The single public API surface for BLUEVERSE clients and backend services."
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

// Validate Auth-issued JWTs at the public API boundary. The signing key is
// shared with the Auth service through deployment secrets, never source code.
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

builder.Services.AddAuthorization();

// CORS Policy
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? new[] { "http://localhost", "http://localhost:5173", "http://localhost:8080", "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("BlueverseCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Forwarded Headers for Edge-Nginx reverse proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// YARP Reverse Proxy - dynamically forwards API requests to sub-services (Auth, AI, Domain)
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Global Exception Handling (RFC 7807 ProblemDetails)
app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc7807",
            title = "API Gateway Error",
            status = StatusCodes.Status500InternalServerError,
            detail = "An unexpected error occurred at the API gateway."
        }, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    });
});

app.UseForwardedHeaders();

app.UseCors("BlueverseCorsPolicy");

// OpenAPI doc for the public API gateway
app.MapOpenApi("/api/swagger/{documentName}.json");

// Swagger UI is hosted only by the public API service. Auth is represented in
// the same UI through its OpenAPI document exposed via the API gateway.
app.UseSwagger(options =>
{
    options.RouteTemplate = "api/swagger/{documentName}/swagger.json";
});

app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "api/swagger";
    options.DocumentTitle = "BLUEVERSE API Documentation";
    options.SwaggerEndpoint("/api/swagger/v1/swagger.json", "BLUEVERSE Public API");
    options.SwaggerEndpoint("/api/auth/swagger/v1/swagger.json", "BLUEVERSE Auth API");
});

app.UseAuthentication();
app.UseAuthorization();

// Route gateway controllers (e.g. /api/health)
app.MapControllers();

// Route all reverse-proxy services (e.g. /api/auth/* to auth service, future services to their clusters)
app.MapReverseProxy();

app.Run();

// Make the implicit Program class public so test projects can access it
public partial class Program { }
