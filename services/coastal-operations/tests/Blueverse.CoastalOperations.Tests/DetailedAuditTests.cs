using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Domain;
using Blueverse.CoastalOperations.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace Blueverse.CoastalOperations.Tests;

public sealed class DetailedAuditTests
{
    [Fact(DisplayName = "COASTAL-AUDIT-DETAIL-001 captured names roles and exact changed fields survive later record edits")]
    [Trait("TestId", "COASTAL-AUDIT-DETAIL-001")]
    public async Task CapturedFieldsRemainHistorical()
    {
        var actor = Guid.NewGuid(); var id = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<CoastalOperationsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new("sub", actor.ToString()), new(ClaimTypes.Name, "Coastal Steward"), new(ClaimTypes.Role, "Field Officer"), new("permission", "operations.audit.read")], "verified")) };
        await using var db = new CoastalOperationsDbContext(options, new HttpContextAccessor { HttpContext = context });
        var record = new Assessment { Id = id, InitiatedBy = actor, Title = "Original review", Objective = "Inspect access", ComponentDependenciesJson = "secret-provider-data" };
        db.Assessments.Add(record); db.OperationsAudit.Add(Event(id, actor, "CREATED")); await db.SaveChangesAsync();
        record.Title = "Updated review"; record.Objective = "Inspect dunes"; record.Version++;
        db.OperationsAudit.Add(Event(id, actor, "DRAFT_UPDATED")); await db.SaveChangesAsync();
        var updated = await db.OperationsAudit.SingleAsync(x => x.Action == "DRAFT_UPDATED");
        Assert.Equal("Coastal Steward", updated.ActorName); Assert.Equal("[\"Field Officer\"]", updated.ActorRolesJson);
        Assert.Equal("Updated review", updated.RecordTitle); Assert.Equal("Updated the draft (assessment)", updated.Summary);
        var changes = JsonSerializer.Deserialize<AuditFieldChange[]>(updated.ChangesJson, OperationsValidation.JsonOptions)!;
        Assert.Contains(changes, x => x.Field == "Title" && x.Before == "Original review" && x.After == "Updated review");
        Assert.Contains(changes, x => x.Field == "Objective" && x.Before == "Inspect access" && x.After == "Inspect dunes");
        Assert.Contains(changes, x => x.Field == "Version" && x.Before == "1" && x.After == "2");
        Assert.DoesNotContain("secret-provider-data", updated.ChangesJson); Assert.DoesNotContain(changes, x => x.Field == "InitiatedBy");
        record.Title = "Later title"; await db.SaveChangesAsync();
        var page = await new OperationsAuditReader(db).GetAssessmentAsync(id, actor, false, new AuditListQuery(), default);
        var detail = Assert.Single(page.Items.Where(x => x.Action == "DRAFT_UPDATED"));
        Assert.Equal("Updated review", detail.RecordTitle); Assert.Equal("Coastal Steward", detail.ActorName); Assert.Equal(changes, detail.Changes);
    }

    [Fact(DisplayName = "COASTAL-AUDIT-DETAIL-002 unrelated actor identity is not attributed and failed save commits neither record nor audit")]
    [Trait("TestId", "COASTAL-AUDIT-DETAIL-002")]
    public async Task AtomicFailureAndScope()
    {
        var interceptor = new FailSave(); var name = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<CoastalOperationsDbContext>().UseInMemoryDatabase(name).AddInterceptors(interceptor).Options;
        var actor = Guid.NewGuid(); var id = Guid.NewGuid();
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new("sub", Guid.NewGuid().ToString()), new(ClaimTypes.Name, "Wrong person"), new(ClaimTypes.Role, "Admin")], "verified")) };
        await using (var db = new CoastalOperationsDbContext(options, new HttpContextAccessor { HttpContext = context }))
        {
            db.Assessments.Add(new Assessment { Id = id, InitiatedBy = actor, Title = "Saved" }); db.OperationsAudit.Add(Event(id, actor, "CREATED")); await db.SaveChangesAsync();
            Assert.Null((await db.OperationsAudit.SingleAsync()).ActorName); Assert.Equal("[]", (await db.OperationsAudit.SingleAsync()).ActorRolesJson);
            var record = await db.Assessments.SingleAsync(); record.Title = "Must not persist";
            db.OperationsAudit.Add(Event(id, actor, "DRAFT_UPDATED")); interceptor.Fail = true;
            await Assert.ThrowsAsync<IOException>(() => db.SaveChangesAsync());
        }
        interceptor.Fail = false;
        await using var fresh = new CoastalOperationsDbContext(options);
        Assert.Equal("Saved", (await fresh.Assessments.SingleAsync()).Title); Assert.Single(await fresh.OperationsAudit.ToListAsync());
        fresh.OperationsAudit.Add(Event(id, Guid.Empty, "EXPIRED")); await fresh.SaveChangesAsync();
        Assert.Equal("System", (await fresh.OperationsAudit.SingleAsync(x => x.Action == "EXPIRED")).ActorName);
    }

    [Theory(DisplayName = "COASTAL-AUDIT-DETAIL-003 signed identity rejects tampering malformed excessive and missing snapshots")]
    [Trait("TestId", "COASTAL-AUDIT-DETAIL-003")]
    [InlineData("valid", true)] [InlineData("tamper", false)] [InlineData("removed", false)] [InlineData("malformed", false)] [InlineData("long-name", false)] [InlineData("many-roles", false)]
    public void IdentityIsBoundToSignature(string scenario, bool expected)
    {
        var key = Enumerable.Range(0, 32).Select(x => (byte)x).ToArray(); var actor = Guid.NewGuid().ToString("N");
        var permissions = Convert.ToBase64String(Encoding.UTF8.GetBytes("[\"operations.audit.read\"]")); var time = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(); var nonce = Guid.NewGuid().ToString("N");
        var original = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new { name = "Coastal Steward", roles = new[] { "Field Officer" } }));
        string? identity = scenario switch { "tamper" => Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"name\":\"Forged\",\"roles\":[\"Admin\"]}")), "removed" => null, "malformed" => "%%%", "long-name" => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new { name = new string('x', 101), roles = Array.Empty<string>() })), "many-roles" => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new { name = "Member", roles = Enumerable.Range(0, 33).Select(x => $"Role{x}").ToArray() })), _ => original };
        var signature = Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(string.Join('\n', "GET", "/api/operations/logs/assessments", actor, permissions, "test", time, nonce, original))));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["COASTAL_OPERATIONS_CONTEXT_KEY"] = Convert.ToBase64String(key) }).Build();
        var accepted = new CoastalActorContextEnvelopeVerifier(config).TryValidate("GET", "/api/operations/logs/assessments", actor, permissions, "test", time, nonce, signature, out var actualActor, out var grants, out _, identity);
        Assert.Equal(expected, accepted); if (accepted) { Assert.Equal(Guid.ParseExact(actor, "N"), actualActor); Assert.Equal(new[] { "operations.audit.read" }, grants); }
    }

    private static OperationsAuditEntry Event(Guid id, Guid actor, string action) => new() { Id = Guid.NewGuid(), ResourceType = "assessment", ResourceId = id, ActorId = actor, Action = action, CorrelationId = "test", CreatedAt = DateTimeOffset.UtcNow };
    private sealed class FailSave : SaveChangesInterceptor
    {
        public bool Fail { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken token = default) => Fail ? throw new IOException("Synthetic failure") : ValueTask.FromResult(result);
    }
}
