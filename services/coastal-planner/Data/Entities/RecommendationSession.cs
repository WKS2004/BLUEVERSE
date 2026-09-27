using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Blueverse.CoastalPlanner.Data.Entities;

[Table("recommendations", Schema = "coastal_planner")]
public class RecommendationSession
{
    [Key]
    public Guid RecommendationId { get; set; }

    [Required]
    public Guid WorkflowId { get; set; }

    public Guid? UserId { get; set; }

    public Guid TargetDestinationId { get; set; }

    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public int DurationHours { get; set; }

    [MaxLength(64)]
    public string ExperienceLevel { get; set; } = "INTERMEDIATE";

    public bool IncludeBiodiversityContext { get; set; }

    public string? CandidatesJson { get; set; }

    public int ExcludedCandidatesCount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
