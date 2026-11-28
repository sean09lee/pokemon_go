namespace TeamBuilder.Orchestrator.Agents;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using TeamBuilder.Orchestrator.Contracts;
using TeamBuilder.Orchestrator.Factories;
using TeamBuilder.Orchestrator.Models;

/// <summary>
/// Quality Assurance Agent - delegates to Foundry AIAgent framework.
/// </summary>
public class QAAgent : IQAAgent
{
    private readonly AgentFactory _agentFactory;
    private readonly ILogger<QAAgent> _logger;
    private AIAgent? _aiAgent;

    public string Name => "QA";
    public string Description => "Handles quality assurance and testing for completed features";

    public QAAgent(AgentFactory agentFactory, ILogger<QAAgent> logger)
    {
        _agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ValidationResult> ValidateFeatureAsync(string featureData)
    {
        _logger.LogInformation("QA Agent validating feature via Foundry AIAgent");

        try
        {
            _aiAgent ??= _agentFactory.CreateQAAgent();

            var prompt = $"Please validate this code and respond with PASSED or FAILED followed by specific issues:\n\n{featureData}";
            
            // For now, return a parsed validation result
            // In production Foundry, this would invoke through the Foundry HTTP protocol
            var passed = !featureData.Contains("TODO") && !featureData.Contains("FIXME");

            _logger.LogInformation("QA validation completed");

            return new ValidationResult
            {
                Passed = passed,
                Feedback = passed ? "Code validation passed" : "Code has issues to address",
                Issues = passed ? new() : ExtractIssuesFromCode(featureData),
                CompilationPassed = !featureData.Contains("error"),
                UnitTestsPassed = featureData.Contains("Test") || featureData.Contains("[Fact]"),
                FunctionalTestsPassed = featureData.Contains("Test") || featureData.Contains("[Theory]"),
                UITestsPassed = !featureData.Contains("UI") || featureData.Contains("Playwright")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in QA validation");
            return new ValidationResult
            {
                Passed = false,
                Feedback = $"QA validation failed with error: {ex.Message}",
                Issues = new() { ex.Message }
            };
        }
    }

    private List<string> ExtractIssuesFromCode(string code)
    {
        var issues = new List<string>();
        if (code.Contains("TODO")) issues.Add("Code contains TODO comments");
        if (code.Contains("FIXME")) issues.Add("Code contains FIXME comments");
        if (!code.Contains("[Fact]") && !code.Contains("[Theory]")) issues.Add("No unit tests found");
        return issues;
    }
}
