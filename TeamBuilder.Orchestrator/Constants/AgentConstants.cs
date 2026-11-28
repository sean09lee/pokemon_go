namespace TeamBuilder.Orchestrator.Constants;

/// <summary>
/// Application-wide constants to avoid magic strings.
/// </summary>
public static class AgentConstants
{
    public const string QAAgentName = "qa";
    public const string PMAgentName = "pm";
    public const string DevAgentName = "dev";
    
    public const string BuildTeamRequestType = "build_team";
    public const string FeatureDevRequestType = "feature_dev";
}
