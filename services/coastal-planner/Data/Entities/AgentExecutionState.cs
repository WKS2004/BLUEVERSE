using System.ComponentModel.DataAnnotations;

namespace Blueverse.CoastalPlanner.Data.Entities;

public sealed class AgentExecutionState
{
    [Key]
    public Guid WorkflowId { get; set; }

    [Required, MaxLength(32)]
    public string Status { get; set; } = "PLANNING";

    [Required, MaxLength(80)]
    public string CorrelationId { get; set; } = string.Empty;

    [Required]
    public string StateJson { get; set; } = "{}";

    public DateTime UpdatedAtUtc { get; set; }
}
