using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Blueverse.CoastalPlanner.Data.Entities;

[Table("biodiversity_predictions_cache", Schema = "coastal_planner")]
public class BiodiversityPredictionCache
{
    [Key]
    public Guid PredictionId { get; set; }

    [Required]
    public Guid DestinationId { get; set; }

    public Guid? ActivityId { get; set; }

    [MaxLength(64)]
    public string Status { get; set; } = "AVAILABLE";

    public string? SpeciesDataJson { get; set; }

    [MaxLength(64)]
    public string ModelVersion { get; set; } = "it3091-v1.2";

    public DateTime InferenceTimestampUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public string? Limitations { get; set; }
}
