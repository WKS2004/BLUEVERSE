using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Data;
using Blueverse.CoastalOperations.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blueverse.CoastalOperations.Controllers;

[ApiController]
[Authorize]
[Route("api/operations/form-options")]
public sealed class FormOptionsController(CoastalOperationsDbContext db, ICoastalReferencePort references) : ControllerBase
{
    [HttpGet]
    [HasPermission("operations.form.options.read")]
    [ProducesResponseType(typeof(OperationsFormOptions), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationsFormOptions>> GetFormOptions(CancellationToken cancellationToken)
    {
        var actor = AuthenticatedActor.GetId(User);
        if (actor == Guid.Empty) throw new CoastalOperationsException(401, "actor_invalid", "Authentication is required", "A valid actor is required.");
        Response.Headers.CacheControl = "no-store";
        var now = DateTimeOffset.UtcNow;
        var locations = await db.TimeZoneLocations.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Country).ThenBy(x => x.Location).ToListAsync(cancellationToken);
        var zones = locations.Select(x =>
        {
            TimeZoneInfo? zone = null;
            try { zone = OperationsTimeZones.Zone(x.Id); }
            catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException) { }
            return new TimeZoneChoice(x.Id, x.Country, x.Location, x.Description, x.SourceVersion,
                zone is null ? null : (int)zone.GetUtcOffset(now).TotalMinutes,
                zone?.SupportsDaylightSavingTime ?? false, zone is not null);
        }).ToArray();
        var canRead = User.HasClaim("permission", CoastalPermissions.AssessmentRead) || User.HasClaim("permission", CoastalPermissions.AssessmentQueueRead);
        var queue = User.HasClaim("permission", CoastalPermissions.AssessmentQueueRead);
        var assessments = canRead ? await db.Assessments.AsNoTracking().Where(x => x.TargetId != Guid.Empty &&
            x.WorkflowStatus != "CANCELLED" && (x.InitiatedBy == actor || (queue && x.WorkflowStatus != "DRAFT")))
            .OrderByDescending(x => x.CreatedAt).Take(100).Select(x => new NamedCoastalReference(x.Id, x.Title, x.TargetType, x.TargetId)).ToArrayAsync(cancellationToken) : [];
        return Ok(new OperationsFormOptions(zones, await references.GetTargetsAsync(actor, cancellationToken),
            await references.GetPlansAsync(actor, cancellationToken), assessments));
    }
}
