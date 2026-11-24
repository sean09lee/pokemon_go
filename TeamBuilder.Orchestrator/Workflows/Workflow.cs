namespace TeamBuilder.Orchestrator.Workflows;

using TeamBuilder.Orchestrator.Contracts;
using TeamBuilder.Orchestrator.Models;

/// <summary>
/// Orchestrates collaboration between QA, PM, and Dev agents.
/// </summary>
public class Workflow(
    IQAAgent qaAgent,
    IPMAgent pmAgent,
    IDevAgent devAgent,
    ILogger<Workflow> logger) : IWorkflow
{
    private readonly IQAAgent _qaAgent = qaAgent ?? throw new ArgumentNullException(nameof(qaAgent));
    private readonly IPMAgent _pmAgent = pmAgent ?? throw new ArgumentNullException(nameof(pmAgent));
    private readonly IDevAgent _devAgent = devAgent ?? throw new ArgumentNullException(nameof(devAgent));
    private readonly ILogger<Workflow> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private const int MaxIterations = 5;

    /// <summary>
    /// Orchestrates feature development workflow.
    /// </summary>
    public async Task<string> FeatureDevelopmentWorkflowAsync(string featureRequest)
    {
        _logger.LogInformation("Starting feature development workflow for: {Feature}", featureRequest);

        try
        {
            var validation = await _pmAgent.ValidateFeatureRequestAsync(featureRequest);
            _logger.LogInformation("PM Validation: {Validation}", validation);

            var code = await _devAgent.GenerateCodeAsync(featureRequest);
            _logger.LogInformation("Dev Code Generation: {Code}", code);

            var qaResult = await ValidateWithFeedbackLoopAsync(code, "feature code");

            return qaResult.Passed
                ? $"Feature development completed successfully. {qaResult.Feedback}"
                : $"Feature development workflow failed after {MaxIterations} attempts. Final feedback: {qaResult.Feedback}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in feature development workflow");
            throw;
        }
    }

    private async Task<ValidationResult> ValidateWithFeedbackLoopAsync(string code, string description)
    {
        string currentCode = code;

        for (int iteration = 1; iteration <= MaxIterations; iteration++)
        {
            _logger.LogInformation("Validation iteration {Iteration}/{MaxIterations} for {Description}", iteration, MaxIterations, description);

            var validationResult = await _qaAgent.ValidateFeatureAsync(currentCode);

            if (validationResult.Passed)
            {
                _logger.LogInformation("Validation passed on iteration {Iteration}", iteration);
                return validationResult;
            }

            if (iteration == MaxIterations)
            {
                _logger.LogWarning("Max iterations ({MaxIterations}) reached. Validation failed.", MaxIterations);
                return validationResult;
            }

            _logger.LogInformation("Validation failed. Issues: {Issues}", string.Join("; ", validationResult.Issues));

            currentCode = await _devAgent.ReviseCodeAsync(currentCode, validationResult.Feedback);
            _logger.LogInformation("Dev revised {Description} based on feedback", description);
        }

        return new ValidationResult { Passed = false, Feedback = "Max iterations reached" };
    }
}
