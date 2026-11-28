namespace TeamBuilder.Orchestrator.Services;

using TeamBuilder.Orchestrator.Constants;
using TeamBuilder.Orchestrator.Contracts;

/// <summary>
/// Implementation of the orchestrator service.
/// </summary>
public class OrchestratorService(
    IWorkflow workflow,
    IQAAgent qaAgent,
    IPMAgent pmAgent,
    IDevAgent devAgent,
    ILogger<OrchestratorService> logger) : IOrchestratorService
{
    private readonly IWorkflow _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
    private readonly IQAAgent _qaAgent = qaAgent ?? throw new ArgumentNullException(nameof(qaAgent));
    private readonly IPMAgent _pmAgent = pmAgent ?? throw new ArgumentNullException(nameof(pmAgent));
    private readonly IDevAgent _devAgent = devAgent ?? throw new ArgumentNullException(nameof(devAgent));
    private readonly ILogger<OrchestratorService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<string> ExecuteFeatureWorkflowAsync(string featureRequest)
    {
        _logger.LogInformation("Executing feature workflow with request: {Request}", featureRequest);
        return await _workflow.FeatureDevelopmentWorkflowAsync(featureRequest);
    }

    public async Task<string> QueryAgentAsync(string agentName, string query)
    {
        _logger.LogInformation("Querying {AgentName} with query: {Query}", agentName, query);

        if (agentName.ToLowerInvariant() == AgentConstants.QAAgentName)
        {
            var result = await _qaAgent.ValidateFeatureAsync(query);
            return $"Validation {(result.Passed ? "Passed" : "Failed")}: {result.Feedback}";
        }

        if (agentName.ToLowerInvariant() == AgentConstants.PMAgentName)
        {
            return await _pmAgent.DefineRequirementsAsync(query);
        }

        if (agentName.ToLowerInvariant() == AgentConstants.DevAgentName)
        {
            return await _devAgent.ImplementFeatureAsync(query);
        }

        throw new ArgumentException($"Unknown agent: {agentName}");
    }
}
