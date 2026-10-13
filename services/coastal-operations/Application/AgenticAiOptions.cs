namespace Blueverse.CoastalOperations.Application;

public sealed class AgenticAiOptions
{
    public bool Enabled { get; set; }
    public string? BaseUrl { get; set; }
    public string? ServiceKey { get; set; }
    public int DispatchTimeoutSeconds { get; set; } = 120;
}
