using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Blueverse.CoastalOperations.Tests;

public sealed class CoastalOperationsStorageAndWorkerTests
{
    [Fact(DisplayName = "COASTAL-EVIDENCE-023 file storage round trips private content and deletes missing or present files safely")]
    [Trait("TestId", "COASTAL-EVIDENCE-023")]
    public async Task FileStorageSupportsRoundTripAndIdempotentDelete()
    {
        var root = NewStorageRoot();
        try
        {
            var storage = new FileSystemAssessmentEvidenceStorage(Options.Create(new EvidenceStorageOptions { RootPath = root }));
            var evidenceId = Guid.NewGuid();
            var expected = new byte[] { 10, 20, 30, 40 };

            Assert.Null(await storage.ReadAsync(evidenceId, CancellationToken.None));
            await storage.StoreAsync(evidenceId, expected, CancellationToken.None);
            Assert.Equal(expected, await storage.ReadAsync(evidenceId, CancellationToken.None));
            Assert.Equal(Path.Combine(root, $"{evidenceId:N}.png"), Assert.Single(Directory.GetFiles(root)));
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite,
                    File.GetUnixFileMode(Path.Combine(root, $"{evidenceId:N}.png")));
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(root));
            }

            await storage.DeleteAsync(evidenceId, CancellationToken.None);
            await storage.DeleteAsync(evidenceId, CancellationToken.None);
            Assert.Null(await storage.ReadAsync(evidenceId, CancellationToken.None));
            Assert.Empty(Directory.GetFiles(root));
        }
        finally
        {
            RemoveStorageRoot(root);
        }
    }

    [Fact(DisplayName = "COASTAL-EVIDENCE-024 canceled and duplicate file writes leave no temporary files or overwrite prior content")]
    [Trait("TestId", "COASTAL-EVIDENCE-024")]
    public async Task FileStorageCleansTemporaryWritesAndPreventsOverwrite()
    {
        var root = NewStorageRoot();
        try
        {
            var storage = new FileSystemAssessmentEvidenceStorage(Options.Create(new EvidenceStorageOptions { RootPath = root }));
            var cancelledId = Guid.NewGuid();
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.StoreAsync(cancelledId, [1, 2], cancelled.Token));
            Assert.Empty(Directory.GetFiles(root));

            var evidenceId = Guid.NewGuid();
            await storage.StoreAsync(evidenceId, [1, 2], CancellationToken.None);
            await Assert.ThrowsAsync<IOException>(() => storage.StoreAsync(evidenceId, [9, 8], CancellationToken.None));
            Assert.Equal(new byte[] { 1, 2 }, await storage.ReadAsync(evidenceId, CancellationToken.None));
            Assert.Single(Directory.GetFiles(root));
        }
        finally
        {
            RemoveStorageRoot(root);
        }
    }

    [Fact(DisplayName = "COASTAL-ALERT-021 hosted expiry worker expires only a bounded batch and writes system audit records")]
    [Trait("TestId", "COASTAL-ALERT-021")]
    public async Task AlertExpiryWorkerProcessesBoundedBatch()
    {
        var databaseName = $"coastal-alert-expiry-{Guid.NewGuid():N}";
        var databaseRoot = new InMemoryDatabaseRoot();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<CoastalOperationsDbContext>(options => options.UseInMemoryDatabase(databaseName, databaseRoot));
        await using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var now = DateTimeOffset.UtcNow;
        await using (var setupScope = scopeFactory.CreateAsyncScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<CoastalOperationsDbContext>();
            db.OperationalAlerts.AddRange(
                Enumerable.Range(0, 101).Select(index => Alert(now.AddMinutes(-index - 1), now.AddMinutes(-index))));
            db.OperationalAlerts.Add(Alert(now.AddMinutes(-1), now.AddHours(1), lifecycle: "ACTIVE"));
            db.OperationalAlerts.Add(Alert(now.AddMinutes(-1), now.AddMinutes(-1), lifecycle: "PROPOSED"));
            await db.SaveChangesAsync();
        }

        var worker = new AlertExpirationHostedService(scopeFactory, NullLogger<AlertExpirationHostedService>.Instance);
        using var waitLimit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await worker.StartAsync(CancellationToken.None);
            while (!waitLimit.IsCancellationRequested)
            {
                await using var verifyScope = scopeFactory.CreateAsyncScope();
                var db = verifyScope.ServiceProvider.GetRequiredService<CoastalOperationsDbContext>();
                if (await db.OperationalAlerts.CountAsync(item => item.Lifecycle == "EXPIRED") == 100) break;
                await Task.Delay(20, waitLimit.Token);
            }
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        await using var finalScope = scopeFactory.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<CoastalOperationsDbContext>();
        Assert.Equal(100, await finalDb.OperationalAlerts.CountAsync(item => item.Lifecycle == "EXPIRED"));
        Assert.Equal(2, await finalDb.OperationalAlerts.CountAsync(item => item.Lifecycle == "ACTIVE"));
        Assert.Equal(1, await finalDb.OperationalAlerts.CountAsync(item => item.Lifecycle == "ACTIVE" && item.ValidUntil <= now));
        Assert.Equal(1, await finalDb.OperationalAlerts.CountAsync(item => item.Lifecycle == "PROPOSED"));
        Assert.Equal(100, await finalDb.AlertDecisions.CountAsync(item => item.Decision == "EXPIRE" && item.ActorId == Guid.Empty));
        Assert.Equal(100, await finalDb.OperationsAudit.CountAsync(item => item.Action == "EXPIRED" && item.CorrelationId == "system:alert-expiry"));
    }

    private static OperationalAlert Alert(DateTimeOffset starts, DateTimeOffset ends, string lifecycle = "ACTIVE") => new()
    {
        Id = Guid.NewGuid(), TargetType = "ACTIVITY", TargetId = Guid.NewGuid(), Title = "Worker test",
        Description = "Synthetic expiration case", Severity = "LOW", Visibility = "PUBLIC", Lifecycle = lifecycle,
        ValidFrom = starts, ValidUntil = ends, CreatedBy = Guid.NewGuid(), UpdatedBy = Guid.NewGuid(),
        Version = 1, CreatedAt = starts, UpdatedAt = starts
    };

    private static string NewStorageRoot() => Path.Combine(Path.GetTempPath(), "blueverse-coastal-evidence", Guid.NewGuid().ToString("N"));

    private static void RemoveStorageRoot(string root)
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        var parent = Path.GetDirectoryName(root);
        if (parent is not null && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
    }
}
