namespace TeamBuilder.Orchestrator.Contracts;

/// <summary>
/// Contract for Dev Agent functionality.
/// </summary>
public interface IDevAgent
{
    /// <summary>
    /// Implements a feature based on requirements.
    /// </summary>
    Task<string> ImplementFeatureAsync(string feature);

    /// <summary>
    /// Generates code based on a specification.
    /// </summary>
    Task<string> GenerateCodeAsync(string specification);

    /// <summary>
    /// Revises code based on QA feedback.
    /// </summary>
    Task<string> ReviseCodeAsync(string code, string feedback);
}

