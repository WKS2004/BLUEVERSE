using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Blueverse.CoastalPlanner.Data.Entities;

[Table("planning_workflows", Schema = "coastal_planner")]
public class PlanningWorkflow
{
    [Key]
    public Guid WorkflowId { get; set; }

    [Required]
    [MaxLength(64)]
    public string WorkflowType { get; set; } = "TOURIST_RECOMMENDATION";

    [Required]
    [MaxLength(64)]
    public string Status { get; set; } = "PENDING"; // PENDING, PROCESSING, COMPLETED, FAILED

    public Guid InitiatorUserId { get; set; }

    [MaxLength(128)]
    public string? Objective { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    public string? ResultSummary { get; set; }
    public string? FailureReason { get; set; }
}
