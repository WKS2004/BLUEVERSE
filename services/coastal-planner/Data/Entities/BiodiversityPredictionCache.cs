using System.ComponentModel.DataAnnotations;

namespace Blueverse.CoastalPlanner.Data.Entities;

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
    public string ModelVersion { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Provider { get; set; } = string.Empty;

    public DateTime InferenceTimestampUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public string? Limitations { get; set; }
    public string? UncertaintyText { get; set; }
}
