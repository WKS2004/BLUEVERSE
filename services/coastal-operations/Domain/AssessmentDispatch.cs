namespace Blueverse.CoastalOperations.Domain;

// Member-owned message delivery state; never an agent execution plan.
public sealed class AssessmentDispatch
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public Guid WorkflowId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = "NOT_CONNECTED";
    public int Attempts { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public Guid ActorId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
