using OpenAI.Chat;

namespace TeamBuilder.Services.AiTools;

/// <summary>
/// Aggregates the available <see cref="IAiTool"/> implementations so the AI Advisor can
/// advertise them to the model and dispatch tool-call requests by name.
/// </summary>
public interface IAiToolRegistry
{
    IReadOnlyList<ChatTool> GetToolDefinitions();

    Task<string> ExecuteAsync(string toolName, string argumentsJson, CancellationToken cancellationToken);
}
