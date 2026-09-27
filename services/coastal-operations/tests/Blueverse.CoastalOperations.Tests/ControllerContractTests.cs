using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Controllers;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Blueverse.CoastalOperations.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Blueverse.CoastalOperations.Tests;

public sealed class ControllerContractTests
{
    [Fact(DisplayName = "COASTAL-CONTROLLER-001 every private controller route has authentication and a registered permission policy")]
    [Trait("TestId", "COASTAL-CONTROLLER-001")]
    public async Task ProtectedActionsDeclareRegisteredAllowAndDenyPolicies()
    {
        var assembly = typeof(AssessmentsController).Assembly;
        var protectedControllers = assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && type != typeof(HealthController))
            .ToArray();
        using var provider = new ServiceCollection().AddLogging().AddAuthorization(CoastalAuthorizationPolicies.Configure).BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var permissions = new HashSet<string>(StringComparer.Ordinal);

        Assert.NotEmpty(protectedControllers);
        foreach (var controller in protectedControllers)
        {
            Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
            Assert.NotNull(controller.GetCustomAttribute<RouteAttribute>());
            var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any())
                .ToArray();
            Assert.NotEmpty(actions);
            foreach (var action in actions)
            {
                var permission = Assert.Single(action.GetCustomAttributes<HasPermissionAttribute>());
                Assert.Equal(permission.PermissionCode, permission.Policy);
                permissions.Add(permission.PermissionCode);
            }
        }

        foreach (var permission in permissions)
        {
            var authorized = new ClaimsPrincipal(new ClaimsIdentity([new Claim("permission", permission)], "test"));
            var denied = new ClaimsPrincipal(new ClaimsIdentity([new Claim("permission", "operations.unrelated")], "test"));
            Assert.True((await authorization.AuthorizeAsync(authorized, null, permission)).Succeeded, permission);
            Assert.False((await authorization.AuthorizeAsync(denied, null, permission)).Succeeded, permission);
        }

        var health = typeof(HealthController);
        Assert.NotNull(health.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(health.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Empty(health.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetCustomAttributes<HasPermissionAttribute>()));
    }

    [Fact(DisplayName = "COASTAL-CONTROLLER-002 invalid actor claims never produce an actor identity")]
    [Trait("TestId", "COASTAL-CONTROLLER-002")]
    public void ActorClaimParsingRequiresAGuid()
    {
        var malformed = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "not-a-guid")], "test"));
        var absent = new ClaimsPrincipal(new ClaimsIdentity([new Claim("permission", "operations.alert.read")], "test"));
        var valid = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", Guid.Empty.ToString())], "test"));

        Assert.Equal(Guid.Empty, AuthenticatedActor.GetId(malformed));
        Assert.Equal(Guid.Empty, AuthenticatedActor.GetId(absent));
        Assert.Equal(Guid.Empty, AuthenticatedActor.GetId(valid));
    }

    [Fact(DisplayName = "COASTAL-CONTROLLER-003 evidence download is private and protected from browser content sniffing")]
    [Trait("TestId", "COASTAL-CONTROLLER-003")]
    public async Task EvidenceDownloadSetsNoStoreAndNosniffHeaders()
    {
        var assessmentId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var bytes = new byte[] { 1, 2, 3, 4 };
        await using var db = new CoastalOperationsDbContext(new DbContextOptionsBuilder<CoastalOperationsDbContext>()
            .UseInMemoryDatabase($"coastal-controller-{Guid.NewGuid():N}").Options);
        db.Assessments.Add(new Assessment
        {
            Id = assessmentId, WorkflowId = Guid.NewGuid(), TargetType = "ACTIVITY", TargetId = Guid.NewGuid(),
            PeriodStartsAt = DateTimeOffset.UtcNow, PeriodEndsAt = DateTimeOffset.UtcNow.AddHours(1),
            Objective = "controller test", WorkflowStatus = "SUBMITTED", InitiatedBy = actorId,
            Version = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        db.AssessmentEvidence.Add(new AssessmentEvidence
        {
            Id = evidenceId, AssessmentId = assessmentId, AssessmentVersion = 1, UploadedBy = actorId,
            MediaType = "image/png", ByteLength = bytes.LongLength,
            ContentSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            InspectionStatus = "AVAILABLE", UploadedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        });
        await db.SaveChangesAsync();
        var storage = new FixedEvidenceStorage(bytes);
        var evidence = new AssessmentEvidenceApplicationService(db, new AssessmentEvidenceSanitizer(), storage,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AssessmentEvidenceApplicationService>.Instance);
        var controller = new AssessmentsController(new AssessmentApplicationService(
            db, new IdempotencyStore(db), new DisconnectedAssessmentProposalPort(), new ControllerTestCollector()))
        {
            ControllerContext = new ControllerContext { HttpContext = NewHttpContext(actorId) }
        };

        var result = Assert.IsType<FileContentResult>(await controller.GetEvidence(assessmentId, evidenceId, evidence, CancellationToken.None));

        Assert.Equal("image/png", result.ContentType);
        Assert.Equal(bytes, result.FileContents);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.Equal("nosniff", controller.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal(1, storage.ReadCalls);
    }

    private static DefaultHttpContext NewHttpContext(Guid actorId)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", actorId.ToString()),
            new Claim("permission", CoastalPermissions.EvidenceRead)
        ], "test"));
        return context;
    }

    private sealed class FixedEvidenceStorage(byte[] content) : IAssessmentEvidenceStorage
    {
        public int ReadCalls { get; private set; }
        public Task StoreAsync(Guid evidenceId, byte[] bytes, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<byte[]?> ReadAsync(Guid evidenceId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCalls++;
            return Task.FromResult<byte[]?>(content.ToArray());
        }
        public Task DeleteAsync(Guid evidenceId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ControllerTestCollector : IComponentDependencyCollector
    {
        public Task<IReadOnlyList<ComponentDependencyResult>> CollectAsync(
            string targetType, Guid targetId, DateTimeOffset periodStartsAt, DateTimeOffset periodEndsAt,
            Guid? sourceWorkflowId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ComponentDependencyResult>>([]);
    }
}
