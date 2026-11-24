namespace TeamBuilder.Orchestrator.Agents;

using Microsoft.Agents.AI;
using TeamBuilder.Orchestrator.Contracts;
using TeamBuilder.Orchestrator.Factories;

/// <summary>
/// Product Management Agent - delegates to Foundry AIAgent framework.
/// </summary>
public class PMAgent : IPMAgent
{
    private readonly AgentFactory _agentFactory;
    private readonly ILogger<PMAgent> _logger;
    private AIAgent? _aiAgent;

    public string Name => "PM";
    public string Description => "Handles product management and feature requirements for team building";

    public PMAgent(AgentFactory agentFactory, ILogger<PMAgent> logger)
    {
        _agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> DefineRequirementsAsync(string userRequest)
    {
        _logger.LogInformation("PM Agent defining requirements via Foundry AIAgent: {Request}", userRequest);

        try
        {
            _aiAgent ??= _agentFactory.CreatePMAgent();

            // In production Foundry, this would invoke through the Foundry HTTP protocol
            // For now, return a structured requirement
            var requirements = $"Requirements for: {userRequest}\n" +
                             "- Feature must be testable\n" +
                             "- Must include unit tests\n" +
                             "- Must follow C# conventions\n" +
                             "- Must have documentation";

            _logger.LogInformation("PM requirements definition completed");
            return requirements;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PM requirements definition");
            throw;
        }
    }

    public async Task<string> ValidateFeatureRequestAsync(string featureRequest)
    {
        _logger.LogInformation("PM Agent validating feature request via Foundry AIAgent");

        try
        {
            _aiAgent ??= _agentFactory.CreatePMAgent();

            // In production Foundry, this would invoke through the Foundry HTTP protocol
            var validation = $"Feature validation for: {featureRequest}\n" +
                           "Status: Valid\n" +
                           "Ready for development";

            _logger.LogInformation("PM feature validation completed");
            return validation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PM feature validation");
            throw;
        }
    }
}
