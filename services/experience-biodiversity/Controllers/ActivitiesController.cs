using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Blueverse.ExperienceBiodiversity.Data;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Models;
using Blueverse.ExperienceBiodiversity.Services;

namespace Blueverse.ExperienceBiodiversity.Controllers;

[ApiController]
[Route("api/experiences/activities")]
public sealed class ActivitiesController : ControllerBase
{
    private readonly ExperienceBiodiversityDbContext _dbContext;
    private readonly IEvaluationService _evaluationService;
    private readonly IUserContext _userContext;
    private readonly ILogger<ActivitiesController> _logger;

    public ActivitiesController(
        ExperienceBiodiversityDbContext dbContext,
        IEvaluationService evaluationService,
        IUserContext userContext,
        ILogger<ActivitiesController> logger)
    {
        _dbContext = dbContext;
        _evaluationService = evaluationService;
        _userContext = userContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? category,
        [FromQuery] string? status,
        CancellationToken cancellationToken = default)
    {
        var q = _dbContext.Activities.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normStatus = status.Trim().ToUpperInvariant();
            if (normStatus != PublicationStatus.Published &&
                !_userContext.HasAnyPermission("experiences.catalogue.read", "experiences.catalogue.manage", "auth.role.system.manage"))
            {
                return Forbid();
            }
            q = q.Where(a => a.Status == normStatus);
        }
        else
        {
            q = q.Where(a => a.Status == PublicationStatus.Published);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var normCat = category.Trim().ToLower();
            q = q.Where(a => a.Category != null && a.Category.ToLower().Contains(normCat));
        }

        var items = await q.OrderBy(a => a.Name)
            .Select(a => new ActivityDto(
                a.Id,
                a.Code,
                a.Name,
                a.Description,
                a.Category,
                a.Status,
                a.CreatedAt,
                a.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var act = await _dbContext.Activities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (act == null)
        {
            return NotFound(new { type = "https://tools.ietf.org/html/rfc7807", title = "Activity Not Found", status = 404, detail = $"Activity with ID {id} does not exist." });
        }

        return Ok(new ActivityDto(
            act.Id,
            act.Code,
            act.Name,
            act.Description,
            act.Category,
            act.Status,
            act.CreatedAt,
            act.UpdatedAt));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateActivityRequest request, CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated)
        {
            return Unauthorized(new { type = "https://tools.ietf.org/html/rfc7807", title = "Unauthorized", status = 401, detail = "Authentication is required to create activities." });
        }

        if (!_userContext.HasAnyPermission("experiences.catalogue.manage", "auth.role.system.manage"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { type = "https://tools.ietf.org/html/rfc7807", title = "Forbidden", status = 403, detail = "You do not have permission to manage activities." });
        }

        var code = request.Code.Trim().ToUpperInvariant();
        var exists = await _dbContext.Activities.AnyAsync(a => a.Code == code, cancellationToken);
        if (exists)
        {
            return Conflict(new { type = "https://tools.ietf.org/html/rfc7807", title = "Activity Code Exists", status = 409, detail = $"An activity with code '{code}' already exists." });
        }

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Category = request.Category?.Trim(),
            Status = PublicationStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Activities.Add(activity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new ActivityDto(
            activity.Id,
            activity.Code,
            activity.Name,
            activity.Description,
            activity.Category,
            activity.Status,
            activity.CreatedAt,
            activity.UpdatedAt);

        return CreatedAtAction(nameof(GetById), new { id = activity.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateActivityRequest request, CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated)
        {
            return Unauthorized(new { type = "https://tools.ietf.org/html/rfc7807", title = "Unauthorized", status = 401, detail = "Authentication is required to update activities." });
        }

        if (!_userContext.HasAnyPermission("experiences.catalogue.manage", "auth.role.system.manage"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { type = "https://tools.ietf.org/html/rfc7807", title = "Forbidden", status = 403, detail = "You do not have permission to manage activities." });
        }

        var act = await _dbContext.Activities.FindAsync(new object[] { id }, cancellationToken);
        if (act == null)
        {
            return NotFound(new { type = "https://tools.ietf.org/html/rfc7807", title = "Activity Not Found", status = 404, detail = $"Activity with ID {id} does not exist." });
        }

        act.Name = request.Name.Trim();
        act.Description = request.Description?.Trim();
        act.Category = request.Category?.Trim();
        act.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ActivityDto(
            act.Id,
            act.Code,
            act.Name,
            act.Description,
            act.Category,
            act.Status,
            act.CreatedAt,
            act.UpdatedAt));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated)
        {
            return Unauthorized(new { type = "https://tools.ietf.org/html/rfc7807", title = "Unauthorized", status = 401, detail = "Authentication is required to delete activities." });
        }

        if (!_userContext.HasAnyPermission("experiences.catalogue.manage", "auth.role.system.manage"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { type = "https://tools.ietf.org/html/rfc7807", title = "Forbidden", status = 403, detail = "You do not have permission to manage activities." });
        }

        var act = await _dbContext.Activities
            .Include(a => a.Offerings)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (act == null)
        {
            return NotFound(new { type = "https://tools.ietf.org/html/rfc7807", title = "Activity Not Found", status = 404, detail = $"Activity with ID {id} does not exist." });
        }

        if (act.Offerings.Count > 0)
        {
            _dbContext.Offerings.RemoveRange(act.Offerings);
        }

        _dbContext.Activities.Remove(act);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:guid}/publication-evaluations")]
    public async Task<IActionResult> EvaluatePublication(Guid id, [FromBody] UpdatePublicationRequest request, CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated)
        {
            return Unauthorized(new { type = "https://tools.ietf.org/html/rfc7807", title = "Unauthorized", status = 401, detail = "Authentication is required to evaluate publications." });
        }

        if (!_userContext.HasAnyPermission("experiences.catalogue.read", "experiences.catalogue.manage", "auth.role.system.manage"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { type = "https://tools.ietf.org/html/rfc7807", title = "Forbidden", status = 403, detail = "You do not have permission to evaluate publications." });
        }

        var eval = await _evaluationService.EvaluateActivityPublicationAsync(id, request.Status, cancellationToken);
        return Ok(eval);
    }

    [HttpPatch("{id:guid}/publication")]
    public async Task<IActionResult> UpdatePublication(Guid id, [FromBody] UpdatePublicationRequest request, CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated)
        {
            return Unauthorized(new { type = "https://tools.ietf.org/html/rfc7807", title = "Unauthorized", status = 401, detail = "Authentication is required to update publications." });
        }

        if (!_userContext.HasAnyPermission("experiences.catalogue.manage", "auth.role.system.manage"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { type = "https://tools.ietf.org/html/rfc7807", title = "Forbidden", status = 403, detail = "You do not have permission to update publications." });
        }

        var eval = await _evaluationService.EvaluateActivityPublicationAsync(id, request.Status, cancellationToken);
        if (!eval.CanTransition)
        {
            return BadRequest(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Publication Transition Blocked",
                status = 400,
                detail = string.Join("; ", eval.Reasons),
                evaluation = eval
            });
        }

        var act = await _dbContext.Activities.FindAsync(new object[] { id }, cancellationToken);
        if (act == null)
        {
            return NotFound(new { type = "https://tools.ietf.org/html/rfc7807", title = "Activity Not Found", status = 404, detail = $"Activity with ID {id} does not exist." });
        }

        act.Status = request.Status.Trim().ToUpperInvariant();
        act.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ActivityDto(
            act.Id,
            act.Code,
            act.Name,
            act.Description,
            act.Category,
            act.Status,
            act.CreatedAt,
            act.UpdatedAt));
    }
}
