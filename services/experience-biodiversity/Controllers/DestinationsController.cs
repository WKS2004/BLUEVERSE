using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Blueverse.ExperienceBiodiversity.Data;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Models;
using Blueverse.ExperienceBiodiversity.Services;

namespace Blueverse.ExperienceBiodiversity.Controllers;

[ApiController]
[Route("api/experiences/destinations")]
public sealed class DestinationsController : ControllerBase
{
    private readonly ExperienceBiodiversityDbContext _dbContext;
    private readonly IEvaluationService _evaluationService;
    private readonly IUserContext _userContext;
    private readonly ILogger<DestinationsController> _logger;

    public DestinationsController(
        ExperienceBiodiversityDbContext dbContext,
        IEvaluationService evaluationService,
        IUserContext userContext,
        ILogger<DestinationsController> logger)
    {
        _dbContext = dbContext;
        _evaluationService = evaluationService;
        _userContext = userContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? query,
        [FromQuery] string? region,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var q = _dbContext.Destinations.AsNoTracking().AsQueryable();

        // Unless an explicit status filter is requested by an authorized caller, default to PUBLISHED
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normStatus = status.Trim().ToUpperInvariant();
            if (normStatus != PublicationStatus.Published &&
                !_userContext.HasAnyPermission("experiences.catalogue.read", "experiences.catalogue.manage", "auth.role.system.manage"))
            {
                return Forbid();
            }
            q = q.Where(d => d.Status == normStatus);
        }
        else
        {
            q = q.Where(d => d.Status == PublicationStatus.Published);
        }

        if (!string.IsNullOrWhiteSpace(region))
        {
            var normRegion = region.Trim().ToLower();
            q = q.Where(d => d.Region != null && d.Region.ToLower().Contains(normRegion));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var normQuery = query.Trim().ToLower();
            q = q.Where(d => d.Name.ToLower().Contains(normQuery) ||
                             (d.Description != null && d.Description.ToLower().Contains(normQuery)));
        }

        var totalCount = await q.CountAsync(cancellationToken);
        var items = await q.OrderBy(d => d.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DestinationDto(
                d.Id,
                d.Name,
                d.Slug,
                d.Description,
                d.Region,
                d.Latitude,
                d.Longitude,
                d.Status,
                d.CreatedAt,
                d.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            total = totalCount,
            page,
            pageSize,
            items
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var d = await _dbContext.Destinations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (d == null)
        {
            return NotFound(new { type = "https://tools.ietf.org/html/rfc7807", title = "Destination Not Found", status = 404, detail = $"Destination with ID {id} does not exist." });
        }

        return Ok(new DestinationDto(
            d.Id,
            d.Name,
            d.Slug,
            d.Description,
            d.Region,
            d.Latitude,
            d.Longitude,
            d.Status,
            d.CreatedAt,
            d.UpdatedAt));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDestinationRequest request, CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated)
        {
            return Unauthorized(new { type = "https://tools.ietf.org/html/rfc7807", title = "Unauthorized", status = 401, detail = "Authentication is required to create destinations." });
        }

        if (!_userContext.HasAnyPermission("experiences.catalogue.manage", "auth.role.system.manage"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { type = "https://tools.ietf.org/html/rfc7807", title = "Forbidden", status = 403, detail = "You do not have permission to manage destinations." });
        }

        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? GenerateSlug(request.Name)
            : GenerateSlug(request.Slug);

        var existingSlug = await _dbContext.Destinations.AnyAsync(d => d.Slug == slug, cancellationToken);
        if (existingSlug)
        {
            slug = $"{slug}-{Guid.NewGuid().ToString("N")[..6]}";
        }

        var destination = new Destination
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Slug = slug,
            Description = request.Description?.Trim(),
            Region = request.Region?.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Status = PublicationStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Destinations.Add(destination);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new DestinationDto(
            destination.Id,
            destination.Name,
            destination.Slug,
            destination.Description,
            destination.Region,
            destination.Latitude,
            destination.Longitude,
            destination.Status,
            destination.CreatedAt,
            destination.UpdatedAt);

        return CreatedAtAction(nameof(GetById), new { id = destination.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDestinationRequest request, CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated)
        {
            return Unauthorized(new { type = "https://tools.ietf.org/html/rfc7807", title = "Unauthorized", status = 401, detail = "Authentication is required to update destinations." });
        }

        if (!_userContext.HasAnyPermission("experiences.catalogue.manage", "auth.role.system.manage"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { type = "https://tools.ietf.org/html/rfc7807", title = "Forbidden", status = 403, detail = "You do not have permission to manage destinations." });
        }

        var dest = await _dbContext.Destinations.FindAsync(new object[] { id }, cancellationToken);
        if (dest == null)
        {
            return NotFound(new { type = "https://tools.ietf.org/html/rfc7807", title = "Destination Not Found", status = 404, detail = $"Destination with ID {id} does not exist." });
        }

        dest.Name = request.Name.Trim();
        if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            dest.Slug = GenerateSlug(request.Slug);
        }
        dest.Description = request.Description?.Trim();
        dest.Region = request.Region?.Trim();
        dest.Latitude = request.Latitude;
        dest.Longitude = request.Longitude;
        dest.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new DestinationDto(
            dest.Id,
            dest.Name,
            dest.Slug,
            dest.Description,
            dest.Region,
            dest.Latitude,
            dest.Longitude,
            dest.Status,
            dest.CreatedAt,
            dest.UpdatedAt));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated)
        {
            return Unauthorized(new { type = "https://tools.ietf.org/html/rfc7807", title = "Unauthorized", status = 401, detail = "Authentication is required to delete destinations." });
        }

        if (!_userContext.HasAnyPermission("experiences.catalogue.manage", "auth.role.system.manage"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { type = "https://tools.ietf.org/html/rfc7807", title = "Forbidden", status = 403, detail = "You do not have permission to manage destinations." });
        }

        var dest = await _dbContext.Destinations.FindAsync(new object[] { id }, cancellationToken);
        if (dest == null)
        {
            return NotFound(new { type = "https://tools.ietf.org/html/rfc7807", title = "Destination Not Found", status = 404, detail = $"Destination with ID {id} does not exist." });
        }

        _dbContext.Destinations.Remove(dest);
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

        var eval = await _evaluationService.EvaluateDestinationPublicationAsync(id, request.Status, cancellationToken);
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

        var eval = await _evaluationService.EvaluateDestinationPublicationAsync(id, request.Status, cancellationToken);
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

        var dest = await _dbContext.Destinations.FindAsync(new object[] { id }, cancellationToken);
        if (dest == null)
        {
            return NotFound(new { type = "https://tools.ietf.org/html/rfc7807", title = "Destination Not Found", status = 404, detail = $"Destination with ID {id} does not exist." });
        }

        dest.Status = request.Status.Trim().ToUpperInvariant();
        dest.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new DestinationDto(
            dest.Id,
            dest.Name,
            dest.Slug,
            dest.Description,
            dest.Region,
            dest.Latitude,
            dest.Longitude,
            dest.Status,
            dest.CreatedAt,
            dest.UpdatedAt));
    }

    [HttpGet("{id:guid}/marine-conditions")]
    public async Task<IActionResult> GetDestinationMarineConditions(
        Guid id,
        [FromServices] IMarineSafetyConsumerService marineSafetyService,
        CancellationToken cancellationToken = default)
    {
        var dest = await _dbContext.Destinations.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (dest == null)
        {
            return NotFound(new { type = "https://tools.ietf.org/html/rfc7807", title = "Destination Not Found", status = 404, detail = $"Destination with ID {id} does not exist." });
        }

        var conditions = await marineSafetyService.GetMarineConditionsAsync(
            dest.Id,
            dest.Name,
            dest.Latitude,
            dest.Longitude,
            cancellationToken);

        return Ok(conditions);
    }

    [HttpGet("{id:guid}/operational-advisories")]
    public async Task<IActionResult> GetDestinationOperationalAdvisories(
        Guid id,
        [FromServices] IOperationalStatusConsumerService opsService,
        CancellationToken cancellationToken = default)
    {
        var dest = await _dbContext.Destinations.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (dest == null)
        {
            return NotFound(new { type = "https://tools.ietf.org/html/rfc7807", title = "Destination Not Found", status = 404, detail = $"Destination with ID {id} does not exist." });
        }

        var advisories = await opsService.GetOperationalAdvisoriesAsync(
            dest.Id,
            dest.Name,
            cancellationToken);

        return Ok(advisories);
    }

    private static string GenerateSlug(string text)
    {
        var clean = text.ToLowerInvariant().Trim();
        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"[^a-z0-9\s-]", "");
        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\s+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(clean) ? "destination" : clean;
    }
}
