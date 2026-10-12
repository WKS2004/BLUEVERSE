using System.ComponentModel.DataAnnotations;

namespace Blueverse.CoastalPlanner.Data.Entities;

public sealed class ItineraryEvaluation
{
    [Key] public Guid EvaluationId { get; set; }
    public Guid ItineraryId { get; set; }
    public DateTime EvaluatedAtUtc { get; set; }
    [Required] public string ResultJson { get; set; } = "";
}
