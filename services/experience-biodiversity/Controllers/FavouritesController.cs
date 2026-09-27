using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Blueverse.ExperienceBiodiversity.Data;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Models;
using Blueverse.ExperienceBiodiversity.Services;

namespace Blueverse.ExperienceBiodiversity.Controllers;

[ApiController]
[Route("api/experiences/favourites")]
public sealed class FavouritesController : ControllerBase
{
    private readonly ExperienceBiodiversityDbContext _dbContext;
    private readonly IUserContext _userContext;
    private readonly ILogger<FavouritesController> _logger;

    public FavouritesController(
        ExperienceBiodiversityDbContext dbContext,
        IUserContext userContext,
        ILogger<FavouritesController> logger)
    {
        _dbContext = dbContext;
        _userContext = userContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetUserFavourites(CancellationToken cancellationToken = default)
    {
        var userId = _userContext.CurrentUserId;
        if (!userId.HasValue)
        {
            return Unauthorized(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Unauthorized",
                status = 401,
                detail = "Authentication is required to view saved experiences."
            });
        }

        var favs = await _dbContext.Favourites
            .AsNoTracking()
            .Where(f => f.UserId == userId.Value)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = new List<FavouriteDto>();
        foreach (var f in favs)
        {
            string? title = null;
            string? status = null;

            if (string.Equals(f.TargetType, "Destination", StringComparison.OrdinalIgnoreCase))
            {
                var d = await _dbContext.Destinations.FindAsync(new object[] { f.TargetId }, cancellationToken);
                title = d?.Name;
                status = d?.Status;
            }
            else if (string.Equals(f.TargetType, "Activity", StringComparison.OrdinalIgnoreCase))
            {
                var a = await _dbContext.Activities.FindAsync(new object[] { f.TargetId }, cancellationToken);
                title = a?.Name;
                status = a?.Status;
            }
            else if (string.Equals(f.TargetType, "Offering", StringComparison.OrdinalIgnoreCase))
            {
                var o = await _dbContext.Offerings.FindAsync(new object[] { f.TargetId }, cancellationToken);
                title = o?.Title;
                status = o?.Status;
            }

            result.Add(new FavouriteDto(f.Id, f.UserId, f.TargetType, f.TargetId, title, status, f.CreatedAt));
        }

        return Ok(result);
    }

    [HttpPut("{targetType}/{targetId:guid}")]
    public async Task<IActionResult> AddFavourite(
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        var userId = _userContext.CurrentUserId;
        if (!userId.HasValue)
        {
            return Unauthorized(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Unauthorized",
                status = 401,
                detail = "Authentication is required to manage saved experiences."
            });
        }

        var normType = NormalizeTargetType(targetType);
        if (normType == null)
        {
            return BadRequest(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Invalid Target Type",
                status = 400,
                detail = "targetType must be one of: Destination, Activity, Offering."
            });
        }

        // Verify target existence and load details
        string? targetTitle = null;
        string? targetStatus = null;

        if (normType == "Destination")
        {
            var d = await _dbContext.Destinations.FindAsync(new object[] { targetId }, cancellationToken);
            targetTitle = d?.Name;
            targetStatus = d?.Status;
        }
        else if (normType == "Activity")
        {
            var a = await _dbContext.Activities.FindAsync(new object[] { targetId }, cancellationToken);
            targetTitle = a?.Name;
            targetStatus = a?.Status;
        }
        else if (normType == "Offering")
        {
            var o = await _dbContext.Offerings.FindAsync(new object[] { targetId }, cancellationToken);
            targetTitle = o?.Title;
            targetStatus = o?.Status;
        }

        if (targetTitle == null)
        {
            return NotFound(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Target Item Not Found",
                status = 404,
                detail = $"The specified {normType} with ID {targetId} does not exist."
            });
        }

        var existing = await _dbContext.Favourites
            .FirstOrDefaultAsync(f => f.UserId == userId.Value && f.TargetType == normType && f.TargetId == targetId, cancellationToken);

        if (existing != null)
        {
            return Ok(new FavouriteDto(existing.Id, existing.UserId, existing.TargetType, existing.TargetId, targetTitle, targetStatus, existing.CreatedAt));
        }

        var fav = new Favourite
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            TargetType = normType,
            TargetId = targetId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Favourites.Add(fav);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new FavouriteDto(fav.Id, fav.UserId, fav.TargetType, fav.TargetId, targetTitle, targetStatus, fav.CreatedAt));
    }

    [HttpDelete("{targetType}/{targetId:guid}")]
    public async Task<IActionResult> RemoveFavourite(
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        var userId = _userContext.CurrentUserId;
        if (!userId.HasValue)
        {
            return Unauthorized(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Unauthorized",
                status = 401,
                detail = "Authentication is required to manage saved experiences."
            });
        }

        var normType = NormalizeTargetType(targetType);
        if (normType == null)
        {
            return BadRequest(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Invalid Target Type",
                status = 400,
                detail = "targetType must be one of: Destination, Activity, Offering."
            });
        }

        var existing = await _dbContext.Favourites
            .FirstOrDefaultAsync(f => f.UserId == userId.Value && f.TargetType == normType && f.TargetId == targetId, cancellationToken);

        if (existing == null)
        {
            return NoContent();
        }

        _dbContext.Favourites.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static string? NormalizeTargetType(string type)
    {
        if (string.Equals(type, "destination", StringComparison.OrdinalIgnoreCase)) return "Destination";
        if (string.Equals(type, "activity", StringComparison.OrdinalIgnoreCase)) return "Activity";
        if (string.Equals(type, "offering", StringComparison.OrdinalIgnoreCase)) return "Offering";
        return null;
    }
}
