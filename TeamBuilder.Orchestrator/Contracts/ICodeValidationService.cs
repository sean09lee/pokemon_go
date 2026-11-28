using TeamBuilder.Orchestrator.Models;

namespace TeamBuilder.Orchestrator.Contracts;

public interface ICodeValidationService
{
    Task<ValidationResult> ValidateCodeAsync(string code, string featureDescription);
}
