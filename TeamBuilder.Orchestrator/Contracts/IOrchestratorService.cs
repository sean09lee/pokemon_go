namespace TeamBuilder.Orchestrator.Contracts;

/// <summary>
/// Service for orchestrating agent interactions and workflows.
/// </summary>
public interface IOrchestratorService
{
    Task<string> ExecuteFeatureWorkflowAsync(string featureRequest);
    Task<string> QueryAgentAsync(string agentName, string query);
}