using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Blueverse.ExperienceBiodiversity.Authorization;
using Blueverse.ExperienceBiodiversity.Data;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Models;
using Blueverse.ExperienceBiodiversity.Services;

namespace Blueverse.ExperienceBiodiversity.Controllers;

[ApiController]
[Authorize]
[Route("api/experiences/offerings")]
public sealed class OfferingsController : ControllerBase
{
    private readonly ExperienceBiodiversityDbContext _dbContext;
    private readonly IEvaluationService _evaluationService;
    private readonly IUserContext _userContext;
    private readonly ILogger<OfferingsController> _logger;

    public OfferingsController(
        ExperienceBiodiversityDbContext dbContext,
        IEvaluationService evaluationService,
        IUserContext userContext,
        ILogger<OfferingsController> logger)
    {
        _dbContext = dbContext;
        _evaluationService = evaluationService;
        _userContext = userContext;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? destinationId,
        [FromQuery] Guid? activityId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var q = _dbContext.Offerings.AsNoTracking()
            .Include(o => o.Destination)
            .Include(o => o.Activity)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normStatus = status.Trim().ToUpperInvariant();
            if (normStatus != PublicationStatus.Published &&
                !_userContext.HasAnyPermission("experiences.catalogue.read", "experiences.catalogue.manage", "auth.role.system.manage"))
            {
                return Forbid();
            }
            q = q.Where(o => o.Status == normStatus);
            if (normStatus == PublicationStatus.Published)
            {
                q = q.Where(o => o.Destination.Status == PublicationStatus.Published &&
                    o.Activity.Status == PublicationStatus.Published);
            }
        }
        else
        {
            q = q.Where(o => o.Status == PublicationStatus.Published &&
                o.Destination.Status == PublicationStatus.Published &&
                o.Activity.Status == PublicationStatus.Published);
        }

        if (destinationId.HasValue)
        {
            q = q.Where(o => o.DestinationId == destinationId.Value);
        }

        if (activityId.HasValue)
        {
            q = q.Where(o => o.ActivityId == activityId.Value);
        }

        var total = await q.CountAsync(cancellationToken);
        var items = await q.OrderBy(o => o.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OfferingDto(
                o.Id,
                o.DestinationId,
                o.Destination.Name,
                o.ActivityId,
                o.Activity.Name,
                o.Activity.Code,
                o.Title,
                o.Description,
                o.Price,
                o.Currency,
                o.DurationMinutes,
                o.MaxCapacity,
                o.Status,
                o.CreatedAt,
                o.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            total,
            page,
            pageSize,
            items
        });
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var o = await _dbContext.Offerings.AsNoTracking()
            .Include(x => x.Destination)
            .Include(x => x.Activity)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (o == null)
        {
            return NotFound(new { type = "https://tools.ietf.org/html/rfc7807", title = "Offering Not Found", status = 404, detail = $"Offering with ID {id} does not exist." });
        }

        if ((o.Status != PublicationStatus.Published ||
             o.Destination.Status != PublicationStatus.Published ||
             o.Activity.Status != PublicationStatus.Published) &&
            !_userContext.HasAnyPermission("experiences.catalogue.read", "experiences.catalogue.manage", "auth.role.system.manage"))
        {
            return NotFound(new { type = "https://tools.ietf.org/html/rfc7807", title = "Offering Not Found", status = 404, detail = $"Offering with ID {id} does not exist." });
        }

        return Ok(new OfferingDto(
            o.Id,
            o.DestinationId,
            o.Destination.Name,
            o.ActivityId,
            o.Activity.Name,
            o.Activity.Code,
            o.Title,
            o.Description,
            o.Price,
            o.Currency,
            o.DurationMinutes,
            o.MaxCapacity,
            o.Status,
            o.CreatedAt,
            o.UpdatedAt));
    }

    [HasPermission("experiences.catalogue.manage")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOfferingRequest request, CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated)
        {
            return Unauthorized(new { type = "https://tools.ietf.org/html/rfc7807", title = "Unauthorized", status = 401, detail = "Authentication is required to create offerings." });
        }

        if (!_userContext.HasAnyPermission("experiences.catalogue.manage", "auth.role.system.manage"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { type = "https://tools.ietf.org/html/rfc7807", title = "Forbidden", status = 403, detail = "You do not have permission to manage offerings." });
        }

        var dest = await _dbContext.Destinations.FindAsync(new object[] { request.DestinationId }, cancellationToken);
        if (dest == null)
        {
            return BadRequest(new { type = "https://tools.ietf.org/html/rfc7807", title = "Invalid Destination", status = 400, detail = $"Destination with ID {request.DestinationId} does not exist." });
        }

        var act = await _dbContext.Activities.FindAsync(new object[] { request.ActivityId }, cancellationToken);
        if (act == null)
        {
            return BadRequest(new { type = "https://tools.ietf.org/html/rfc7807", title = "Invalid Activity", status = 400, detail = $"Activity with ID {request.ActivityId} does not exist." });
        }

        var offering = new Offering
        {
            Id = Guid.NewGuid(),
            DestinationId = request.DestinationId,
            ActivityId = request.ActivityId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Price = request.Price,
            Currency = request.Currency?.Trim().ToUpperInvariant() ?? "USD",
            DurationMinutes = request.DurationMinutes,
            MaxCapacity = request.MaxCapacity,
            Status = PublicationStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Offerings.Add(offering);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new OfferingDto(
            offering.Id,
            dest.Id,
            dest.Name,
            act.Id,
            act.Name,
            act.Code,
            offering.Title,
            offering.Description,
            offering.Price,
            offering.Currency,
            offering.DurationMinutes,
            offering.MaxCapacity,
            offering.Status,
            offering.CreatedAt,
            offering.UpdatedAt);

        return CreatedAtAction(nameof(GetById), new { id = offering.Id }, dto);
    }

    [HasPermission("experiences.catalogue.manage")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOfferingRequest request, CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated)
        {
            return Unauthorized(new { type = "https://tools.ietf.org/html/rfc7807", title = "Unauthorized", status = 401, detail = "Authentication is required to update offerings." });
        }

        if (!_userContext.HasAnyPermission("experiences.catalogue.manage", "auth.role.system.manage"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { type = "https://tools.ietf.org/html/rfc7807", title = "Forbidden", status = 403, detail = "You do not have permission to manage offerings." });
        }

        var offering = await _dbContext.Offerings
            .Include(o => o.Destination)
            .Include(o => o.Activity)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (offering == null)
        {
            return NotFound(new { type = "https://tools.ietf.org/html/rfc7807", title = "Offering Not Found", status = 404, detail = $"Offering with ID {id} does not exist." });
        }

        offering.Title = request.Title.Trim();
        offering.Description = request.Description?.Trim();
        offering.Price = request.Price;
        offering.Currency = request.Currency?.Trim().ToUpperInvariant() ?? offering.Currency;
        offering.DurationMinutes = request.DurationMinutes;
        offering.MaxCapacity = request.MaxCapacity;
        offering.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new OfferingDto(
            offering.Id,
            offering.DestinationId,
            offering.Destination.Name,
            offering.ActivityId,
            offering.Activity.Name,
            offering.Activity.Code,
            offering.Title,
            offering.Description,
            offering.Price,
            offering.Currency,
            offering.DurationMinutes,
            offering.MaxCapacity,
            offering.Status,
            offering.CreatedAt,
            offering.UpdatedAt));
    }

    [HasPermission("experiences.catalogue.manage")]
    [HttpPost("{id:guid}/publication-evaluations")]
    public async Task<IActionResult> EvaluatePublication(Guid id, [FromBody] UpdatePublicationRequest request, CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated)
        {
            return Unauthorized(new { type = "https://tools.ietf.org/html/rfc7807", title = "Unauthorized", status = 401, detail = "Authentication is required to evaluate publications." });
        }

        if (!_userContext.HasAnyPermission("experiences.catalogue.manage", "auth.role.system.manage"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { type = "https://tools.ietf.org/html/rfc7807", title = "Forbidden", status = 403, detail = "You do not have permission to evaluate publications." });
        }

        var eval = await _evaluationService.EvaluateOfferingPublicationAsync(id, request.Status, cancellationToken);
        return Ok(eval);
    }

    [HasPermission("experiences.catalogue.manage")]
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

        var eval = await _evaluationService.EvaluateOfferingPublicationAsync(id, request.Status, cancellationToken);
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

        var offering = await _dbContext.Offerings
            .Include(o => o.Destination)
            .Include(o => o.Activity)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (offering == null)
        {
            return NotFound(new { type = "https://tools.ietf.org/html/rfc7807", title = "Offering Not Found", status = 404, detail = $"Offering with ID {id} does not exist." });
        }

        offering.Status = request.Status.Trim().ToUpperInvariant();
        offering.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new OfferingDto(
            offering.Id,
            offering.DestinationId,
            offering.Destination.Name,
            offering.ActivityId,
            offering.Activity.Name,
            offering.Activity.Code,
            offering.Title,
            offering.Description,
            offering.Price,
            offering.Currency,
            offering.DurationMinutes,
            offering.MaxCapacity,
            offering.Status,
            offering.CreatedAt,
            offering.UpdatedAt));
    }

    [HasPermission("experiences.catalogue.manage")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated)
        {
            return Unauthorized(new { type = "https://tools.ietf.org/html/rfc7807", title = "Unauthorized", status = 401, detail = "Authentication is required to delete offerings." });
        }

        if (!_userContext.HasAnyPermission("experiences.catalogue.manage", "auth.role.system.manage"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { type = "https://tools.ietf.org/html/rfc7807", title = "Forbidden", status = 403, detail = "You do not have permission to manage offerings." });
        }

        var offering = await _dbContext.Offerings.FindAsync(new object[] { id }, cancellationToken);
        if (offering == null)
        {
            return NotFound(new { type = "https://tools.ietf.org/html/rfc7807", title = "Offering Not Found", status = 404, detail = $"Offering with ID {id} does not exist." });
        }

        _dbContext.Offerings.Remove(offering);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    // --- Schedules ---

    [AllowAnonymous]
    [HttpGet("{id:guid}/schedules")]
    public async Task<IActionResult> GetSchedules(Guid id, CancellationToken cancellationToken = default)
    {
        var offering = await _dbContext.Offerings
            .AsNoTracking()
            .Include(item => item.Destination)
            .Include(item => item.Activity)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (offering == null ||
            ((offering.Status != PublicationStatus.Published ||
              offering.Destination.Status != PublicationStatus.Published ||
              offering.Activity.Status != PublicationStatus.Published) &&
             !_userContext.HasAnyPermission("experiences.catalogue.read", "experiences.catalogue.manage", "auth.role.system.manage")))
        {
            return NotFound(new { type = "https://tools.ietf.org/html/rfc7807", title = "Offering Not Found", status = 404, detail = $"Offering with ID {id} does not exist." });
        }

        var schedules = await _dbContext.Schedules
            .AsNoTracking()
            .Where(s => s.OfferingId == id)
            .OrderBy(s => s.StartsAt)
            .Select(s => new ScheduleDto(
                s.Id,
                s.OfferingId,
                s.StartsAt,
                s.EndsAt,
                s.TimeZoneId,
                s.IsActive,
                s.CreatedAt,
                s.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(schedules);
    }

    [HasPermission("experiences.catalogue.manage")]
    [HttpPost("{id:guid}/schedules")]
    public async Task<IActionResult> AddSchedule(Guid id, [FromBody] CreateScheduleRequest request, CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated)
        {
            return Unauthorized(new { type = "https://tools.ietf.org/html/rfc7807", title = "Unauthorized", status = 401, detail = "Authentication is required to add schedules." });
        }

        if (!_userContext.HasAnyPermission("experiences.catalogue.manage", "auth.role.system.manage"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { type = "https://tools.ietf.org/html/rfc7807", title = "Forbidden", status = 403, detail = "You do not have permission to manage schedules." });
        }

        if (request.EndsAt <= request.StartsAt)
        {
            return BadRequest(new { type = "https://tools.ietf.org/html/rfc7807", title = "Invalid Schedule Interval", status = 400, detail = "EndsAt must be strictly greater than StartsAt." });
        }

        var offeringExists = await _dbContext.Offerings.AnyAsync(o => o.Id == id, cancellationToken);
        if (!offeringExists)
        {
            return NotFound(new { type = "https://tools.ietf.org/html/rfc7807", title = "Offering Not Found", status = 404, detail = $"Offering with ID {id} does not exist." });
        }

        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            OfferingId = id,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? "Asia/Colombo" : request.TimeZoneId.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Schedules.Add(schedule);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new ScheduleDto(
            schedule.Id,
            schedule.OfferingId,
            schedule.StartsAt,
            schedule.EndsAt,
            schedule.TimeZoneId,
            schedule.IsActive,
            schedule.CreatedAt,
            schedule.UpdatedAt);

        return CreatedAtAction(nameof(GetSchedules), new { id }, dto);
    }

    [HasPermission("experiences.catalogue.manage")]
    [HttpPut("{id:guid}/schedules/{scheduleId:guid}")]
    public async Task<IActionResult> UpdateSchedule(Guid id, Guid scheduleId, [FromBody] UpdateScheduleRequest request, CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated)
        {
            return Unauthorized(new { type = "https://tools.ietf.org/html/rfc7807", title = "Unauthorized", status = 401, detail = "Authentication is required to update schedules." });
        }

        if (!_userContext.HasAnyPermission("experiences.catalogue.manage", "auth.role.system.manage"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { type = "https://tools.ietf.org/html/rfc7807", title = "Forbidden", status = 403, detail = "You do not have permission to manage schedules." });
        }

        if (request.EndsAt <= request.StartsAt)
        {
            return BadRequest(new { type = "https://tools.ietf.org/html/rfc7807", title = "Invalid Schedule Interval", status = 400, detail = "EndsAt must be strictly greater than StartsAt." });
        }

        var schedule = await _dbContext.Schedules.FirstOrDefaultAsync(s => s.Id == scheduleId && s.OfferingId == id, cancellationToken);
        if (schedule == null)
        {
            return NotFound(new { type = "https://tools.ietf.org/html/rfc7807", title = "Schedule Not Found", status = 404, detail = $"Schedule with ID {scheduleId} was not found on offering {id}." });
        }

        schedule.StartsAt = request.StartsAt;
        schedule.EndsAt = request.EndsAt;
        schedule.TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? schedule.TimeZoneId : request.TimeZoneId.Trim();
        schedule.IsActive = request.IsActive;
        schedule.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ScheduleDto(
            schedule.Id,
            schedule.OfferingId,
            schedule.StartsAt,
            schedule.EndsAt,
            schedule.TimeZoneId,
            schedule.IsActive,
            schedule.CreatedAt,
            schedule.UpdatedAt));
    }

    [HasPermission("experiences.catalogue.manage")]
    [HttpDelete("{id:guid}/schedules/{scheduleId:guid}")]
    public async Task<IActionResult> DeleteSchedule(Guid id, Guid scheduleId, CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated)
        {
            return Unauthorized(new { type = "https://tools.ietf.org/html/rfc7807", title = "Unauthorized", status = 401, detail = "Authentication is required to delete schedules." });
        }

        if (!_userContext.HasAnyPermission("experiences.catalogue.manage", "auth.role.system.manage"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { type = "https://tools.ietf.org/html/rfc7807", title = "Forbidden", status = 403, detail = "You do not have permission to manage schedules." });
        }

        var schedule = await _dbContext.Schedules.FirstOrDefaultAsync(s => s.Id == scheduleId && s.OfferingId == id, cancellationToken);
        if (schedule == null)
        {
            return NotFound(new { type = "https://tools.ietf.org/html/rfc7807", title = "Schedule Not Found", status = 404, detail = $"Schedule with ID {scheduleId} was not found on offering {id}." });
        }

        _dbContext.Schedules.Remove(schedule);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
