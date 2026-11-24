namespace TeamBuilder.Orchestrator.Contracts;

/// <summary>
/// Contract for PM Agent functionality.
/// </summary>
public interface IPMAgent
{
    /// <summary>
    /// Defines requirements based on a user request.
    /// </summary>
    Task<string> DefineRequirementsAsync(string userRequest);

    /// <summary>
    /// Validates a feature request.
    /// </summary>
    Task<string> ValidateFeatureRequestAsync(string featureRequest);
}
