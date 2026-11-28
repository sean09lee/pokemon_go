namespace TeamBuilder.Orchestrator.Models;

/// <summary>
/// Result of a QA validation, including whether it passed and any feedback.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether the validation passed.
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Feedback from the QA agent (issues found or approval message).
    /// </summary>
    public string Feedback { get; set; } = string.Empty;

    /// <summary>
    /// Specific issues or requirements that need to be addressed.
    /// </summary>
    public List<string> Issues { get; set; } = new();

    /// <summary>
    /// Whether the code compiles successfully.
    /// </summary>
    public bool CompilationPassed { get; set; }

    /// <summary>
    /// Whether unit tests pass and cover key scenarios.
    /// </summary>
    public bool UnitTestsPassed { get; set; }

    /// <summary>
    /// Whether functional tests (happy path and edge cases) pass.
    /// </summary>
    public bool FunctionalTestsPassed { get; set; }

    /// <summary>
    /// Whether UI/Playwright tests pass (if applicable).
    /// </summary>
    public bool UITestsPassed { get; set; }

    /// <summary>
    /// Details about compilation issues if any.
    /// </summary>
    public List<string> CompilationErrors { get; set; } = new();

    /// <summary>
    /// Details about failing unit tests.
    /// </summary>
    public List<string> FailingUnitTests { get; set; } = new();

    /// <summary>
    /// Details about failing functional tests.
    /// </summary>
    public List<string> FailingFunctionalTests { get; set; } = new();

    /// <summary>
    /// Details about failing UI tests.
    /// </summary>
    public List<string> FailingUITests { get; set; } = new();
}
