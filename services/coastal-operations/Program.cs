using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Blueverse.CoastalOperations.Data;

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

// Database
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

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseSwagger(options =>
{
    options.RouteTemplate = "api/operations/swagger/{documentName}/swagger.json";
});

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
