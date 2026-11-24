namespace TeamBuilder.Orchestrator.CQRS.Response;

/// <summary>
/// Response model from agent.
/// </summary>
public class AgentResponse
{
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; }
}
