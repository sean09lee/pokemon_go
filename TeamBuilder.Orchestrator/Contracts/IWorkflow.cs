namespace TeamBuilder.Orchestrator.Contracts;

/// <summary>
/// Contract for the orchestration workflow.
/// </summary>
public interface IWorkflow
{
    /// <summary>
    /// Orchestrates a feature development workflow.
    /// </summary>
    Task<string> FeatureDevelopmentWorkflowAsync(string featureRequest);
}
