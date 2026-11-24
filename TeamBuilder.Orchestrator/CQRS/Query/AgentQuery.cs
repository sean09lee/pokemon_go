using TeamBuilder.Orchestrator.Constants;

namespace TeamBuilder.Orchestrator.CQRS.Query;

/// <summary>
/// Request model for agent interactions.
/// </summary>
public class AgentRequest
{
    public string Message { get; set; } = string.Empty;
    public string RequestType { get; set; } = AgentConstants.BuildTeamRequestType;
}
