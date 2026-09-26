using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Blueverse.ExperienceBiodiversity.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

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

            logger.LogInformation("Experience & Biodiversity database schema is ready.");
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
        logger.LogError(
            "Unhandled Experience & Biodiversity request failure for {Method} {Path}",
            context.Request.Method,
            context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(
            new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "An internal error occurred",
                status = StatusCodes.Status500InternalServerError,
                detail = "An unexpected error occurred processing your request."
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    });
});

app.UseSwagger(options =>
{
    options.RouteTemplate = "api/experiences/swagger/{documentName}/swagger.json";
});

app.MapControllers();

app.Run();

public partial class Program { }
