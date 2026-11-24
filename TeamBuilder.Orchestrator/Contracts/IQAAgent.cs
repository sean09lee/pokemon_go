namespace TeamBuilder.Orchestrator.Contracts;

using TeamBuilder.Orchestrator.Models;

/// <summary>
/// Contract for QA Agent functionality.
/// </summary>
public interface IQAAgent
{
    /// <summary>
    /// Validates a feature and returns structured feedback.
    /// </summary>
    Task<ValidationResult> ValidateFeatureAsync(string featureData);
}

