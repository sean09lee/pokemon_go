using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace TeamBuilder.Services.AiTools;

/// <summary>
/// Aggregates the available <see cref="IAiTool"/> implementations so the AI Advisor can
/// advertise them to the model and dispatch tool-call requests by name.
/// </summary>
public class AiToolRegistry(IEnumerable<IAiTool> tools, ILogger<AiToolRegistry> logger) : IAiToolRegistry
{
    private readonly IReadOnlyDictionary<string, IAiTool> _toolsByName = tools.ToDictionary(t => t.Name, t => t);

    public IReadOnlyList<ChatTool> GetToolDefinitions() =>
        _toolsByName.Values.Select(t => t.Definition).ToList();

    public async Task<string> ExecuteAsync(string toolName, string argumentsJson, CancellationToken cancellationToken)
    {
        if (!_toolsByName.TryGetValue(toolName, out var tool))
        {
            logger.LogWarning("AI Advisor requested unknown tool {ToolName}", toolName);
            return JsonSerializer.Serialize(new { error = $"Unknown tool '{toolName}'." });
        }

        logger.LogInformation("Invoking AI tool {ToolName} with arguments {Arguments}", toolName, argumentsJson);

        try
        {
            var result = await tool.InvokeAsync(argumentsJson, cancellationToken);
            logger.LogInformation("AI tool {ToolName} completed successfully", toolName);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI tool {ToolName} failed with arguments {Arguments}", toolName, argumentsJson);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}
