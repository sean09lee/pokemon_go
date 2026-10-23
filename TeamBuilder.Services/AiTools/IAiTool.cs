using OpenAI.Chat;

namespace TeamBuilder.Services.AiTools;

/// <summary>
/// Represents a single function-calling tool that the AI Advisor can invoke to retrieve
/// grounded data from TeamBuilder's Pokemon services instead of relying on the model's
/// general knowledge.
/// </summary>
public interface IAiTool
{
    /// <summary>
    /// The function name exposed to the model. Must match <see cref="Definition"/>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The OpenAI/Azure AI Foundry function-tool definition, including its JSON schema parameters.
    /// </summary>
    ChatTool Definition { get; }

    /// <summary>
    /// Executes the tool using the JSON arguments supplied by the model and returns a JSON
    /// result string to feed back into the conversation.
    /// </summary>
    Task<string> InvokeAsync(string argumentsJson, CancellationToken cancellationToken);
}
