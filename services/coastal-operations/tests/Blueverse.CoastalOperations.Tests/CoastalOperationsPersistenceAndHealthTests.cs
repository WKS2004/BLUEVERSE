using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Controllers;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Blueverse.CoastalOperations.Tests;

public sealed class CoastalOperationsPersistenceAndHealthTests
{
    [Fact(DisplayName = "COASTAL-PERSISTENCE-001 relational model declares the Coastal Operations schema and integrity constraints")]
    [Trait("TestId", "COASTAL-PERSISTENCE-001")]
    public void RelationalModelCarriesCriticalKeysIndexesAndConstraints()
    {
        using var db = CreatePostgresModelContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var assessment = Entity<Assessment>(model);
        var evidence = Entity<AssessmentEvidence>(model);
        var proposal = Entity<AssessmentProposal>(model);
        var decisions = Entity<ReviewerDecision>(model);
        var targetState = Entity<TargetOperationalState>(model);
        var alerts = Entity<OperationalAlert>(model);
        var idempotency = Entity<IdempotencyRecord>(model);

        Assert.Equal("coastal_operations", assessment.GetSchema());
        Assert.Equal("Assessments", assessment.GetTableName());
        Assert.Contains("CK_Assessments_Period", assessment.GetCheckConstraints().Select(item => item.Name));
        Assert.Contains("CK_AssessmentEvidence_ByteLength", evidence.GetCheckConstraints().Select(item => item.Name));
        Assert.Contains("CK_AssessmentProposals_Validity", proposal.GetCheckConstraints().Select(item => item.Name));
        Assert.Contains("CK_ReviewerDecisions_Decision", decisions.GetCheckConstraints().Select(item => item.Name));
        Assert.Contains("CK_TargetOperationalStates_State", targetState.GetCheckConstraints().Select(item => item.Name));
        Assert.Contains("CK_OperationalAlerts_Visibility", alerts.GetCheckConstraints().Select(item => item.Name));
        Assert.True(HasUniqueIndex(idempotency, nameof(IdempotencyRecord.ActorId), nameof(IdempotencyRecord.Operation), nameof(IdempotencyRecord.Key)));
        Assert.True(HasUniqueIndex(targetState, nameof(TargetOperationalState.TargetType), nameof(TargetOperationalState.TargetId)));
        Assert.True(HasUniqueIndex(decisions, nameof(ReviewerDecision.ProposalId), nameof(ReviewerDecision.ProposalVersion)));
        Assert.True(HasUniqueIndex(proposal, nameof(AssessmentProposal.AssessmentId), nameof(AssessmentProposal.ProposalVersion)));
        Assert.True(assessment.FindProperty(nameof(Assessment.Version))!.IsConcurrencyToken);
        Assert.True(evidence.FindProperty(nameof(AssessmentEvidence.InspectionStatus))!.IsConcurrencyToken);
    }

    [Fact(DisplayName = "COASTAL-PERSISTENCE-002 migration assembly contains the schema evolution chain")]
    [Trait("TestId", "COASTAL-PERSISTENCE-002")]
    public void MigrationsAreDiscoverableWithoutDatabaseAccess()
    {
        using var db = CreatePostgresModelContext();

        var migrations = db.Database.GetMigrations().ToArray();

        Assert.NotEmpty(migrations);
        Assert.Contains(migrations, name => name.EndsWith("CoastalOperationsInitialSchema", StringComparison.Ordinal));
        Assert.Contains(migrations, name => name.EndsWith("CoastalOperationsIntegrity", StringComparison.Ordinal));
        Assert.Contains(migrations, name => name.EndsWith("PersistComponentDependencyResults", StringComparison.Ordinal));
        Assert.Contains(migrations, name => name.EndsWith("CoastalOperationsEvidenceAndAlertVisibility", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "COASTAL-HEALTH-001 readiness reports an unavailable PostgreSQL dependency while liveness stays healthy")]
    [Trait("TestId", "COASTAL-HEALTH-001")]
    public async Task DatabaseFailureAffectsReadinessButNotLiveness()
    {
        var options = new DbContextOptionsBuilder<CoastalOperationsDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=coastal_health_probe;Username=test;Password=test;Timeout=1;Command Timeout=1;Pooling=false")
            .Options;
        await using var db = new CoastalOperationsDbContext(options);
        var registry = new ComponentDependencyHealthRegistry();
        registry.Record(new ComponentDependencyResult(
            "member-1-experience", "experience-availability", "UNAVAILABLE", 2, 1, true,
            "SERVICE_UNREACHABLE", "The peer is not available.", DateTimeOffset.UtcNow, null));
        var controller = new HealthController(db, registry)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var liveness = Assert.IsType<OkObjectResult>(controller.Live());
        var readiness = Assert.IsType<ObjectResult>(await controller.Ready());
        var payload = readiness.Value!;

        Assert.Equal(StatusCodes.Status200OK, liveness.StatusCode);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, readiness.StatusCode);
        Assert.Equal("unhealthy", Property<string>(payload, "status"));
        Assert.Equal("unavailable", Property<string>(payload, "database"));
        Assert.Equal("UNAVAILABLE", Assert.Single(Property<IReadOnlyList<ComponentDependencyHealth>>(payload, "componentDependencies"), item => item.Service == "member-1-experience").Status);
    }

    private static CoastalOperationsDbContext CreatePostgresModelContext() => new(
        new DbContextOptionsBuilder<CoastalOperationsDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only;Username=model-only;Password=model-only")
            .Options);

    private static Microsoft.EntityFrameworkCore.Metadata.IEntityType Entity<TEntity>(Microsoft.EntityFrameworkCore.Metadata.IModel model) =>
        Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IEntityType>(model.FindEntityType(typeof(TEntity)));

    private static bool HasUniqueIndex(
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entity,
        params string[] propertyNames) => entity.GetIndexes().Any(index =>
        index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(propertyNames));

    private static T Property<T>(object value, string name) =>
        (T)value.GetType().GetProperty(name)!.GetValue(value)!;
}
