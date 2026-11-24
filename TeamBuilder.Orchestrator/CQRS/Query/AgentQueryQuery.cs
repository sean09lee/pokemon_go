namespace TeamBuilder.Orchestrator.CQRS.Query;

/// <summary>
/// Request model for querying a specific agent.
/// </summary>
public class AgentQueryQuery
{
    public string Agent { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
}