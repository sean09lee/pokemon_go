using Microsoft.AspNetCore.Mvc;
using TeamBuilder.Orchestrator.Contracts;
using TeamBuilder.Orchestrator.CQRS.Query;
using TeamBuilder.Orchestrator.CQRS.Response;

namespace TeamBuilder.Orchestrator.Controllers;

public class AgentController(IOrchestratorService orchestratorService, ILogger<AgentController> logger) : ControllerBase
{
    private readonly IOrchestratorService _orchestratorService = orchestratorService ?? throw new ArgumentNullException(nameof(orchestratorService));
    private readonly ILogger<AgentController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Feature development endpoint for agent interaction.
    /// </summary>
    [HttpPost("feature")]
    public async Task<IActionResult> FeatureDevelopment([FromBody] AgentRequest request)
    {
        try
        {
            _logger.LogInformation("Received feature request: {Message}", request.Message);
            string result = await _orchestratorService.ExecuteFeatureWorkflowAsync(request.Message);
            return Ok(new AgentResponse { Message = result, Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing feature request");
            return BadRequest(new AgentResponse { Message = ex.Message, Success = false });
        }
    }

    /// <summary>
    /// Query a specific agent.
    /// </summary>
    [HttpPost("query")]
    public async Task<IActionResult> QueryAgent([FromBody] AgentQueryQuery request)
    {
        try
        {
            _logger.LogInformation("Querying agent: {Agent}", request.Agent);
            var result = await _orchestratorService.QueryAgentAsync(request.Agent, request.Query);
            return Ok(new AgentResponse { Message = result, Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying agent");
            return BadRequest(new AgentResponse { Message = ex.Message, Success = false });
        }
    }
}

