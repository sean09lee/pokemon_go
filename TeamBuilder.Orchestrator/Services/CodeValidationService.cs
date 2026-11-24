namespace TeamBuilder.Orchestrator.Services;

using System.Text.RegularExpressions;
using TeamBuilder.Orchestrator.Contracts;
using TeamBuilder.Orchestrator.Models;

/// <summary>
/// Service for validating code quality, compilation, and test coverage.
/// </summary>
public class CodeValidationService : ICodeValidationService
{
    private readonly ILogger<CodeValidationService> _logger;
    private const string TestFrameworkPattern = @"(xUnit|NUnit|MSTest|Assert\.|Fact|Theory)";
    private const string MockingFrameworkPattern = @"(Mock|Moq|NSubstitute|FakeItEasy)";

    public CodeValidationService(ILogger<CodeValidationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates code for compilation, unit tests, and functional coverage.
    /// </summary>
    public async Task<ValidationResult> ValidateCodeAsync(string code, string featureDescription)
    {
        var result = new ValidationResult();

        try
        {
            _logger.LogInformation("Starting validation for feature: {Feature}", featureDescription);

            // 1. Check compilation
            result.CompilationPassed = await CheckCompilationAsync(code);
            if (!result.CompilationPassed)
            {
                result.Issues.Add("Code does not compile");
                _logger.LogWarning("Compilation failed for feature: {Feature}", featureDescription);
            }
            else
            {
                _logger.LogInformation("Code compilation successful");
            }

            // 2. Check for unit tests and test scenarios
            var testAnalysis = AnalyzeTestCoverage(code);
            result.UnitTestsPassed = testAnalysis.HasTests;
            result.FailingUnitTests = testAnalysis.MissingScenarios;

            if (!testAnalysis.HasTests)
            {
                result.Issues.Add("No unit tests detected");
                _logger.LogWarning("No unit tests found for feature: {Feature}", featureDescription);
            }
            else if (testAnalysis.MissingScenarios.Count > 0)
            {
                foreach (var missing in testAnalysis.MissingScenarios)
                {
                    result.Issues.Add($"Missing test scenario: {missing}");
                }
                _logger.LogWarning("Missing test scenarios: {Scenarios}", string.Join(", ", testAnalysis.MissingScenarios));
            }
            else
            {
                _logger.LogInformation("Unit test coverage looks adequate");
            }

            // 3. Check for functional test scenarios (valid, invalid, edge cases)
            var functionalAnalysis = AnalyzeFunctionalScenarios(code, featureDescription);
            result.FunctionalTestsPassed = functionalAnalysis.HasRequiredScenarios;
            result.FailingFunctionalTests = functionalAnalysis.MissingScenarios;

            if (!functionalAnalysis.HasRequiredScenarios)
            {
                foreach (var missing in functionalAnalysis.MissingScenarios)
                {
                    result.Issues.Add($"Missing functional test: {missing}");
                }
                _logger.LogWarning("Missing functional scenarios: {Scenarios}", string.Join(", ", functionalAnalysis.MissingScenarios));
            }
            else
            {
                _logger.LogInformation("Functional test scenarios present");
            }

            // 4. Check for UI/Playwright tests if UI-related
            var uiAnalysis = AnalyzeUITestCoverage(code, featureDescription);
            result.UITestsPassed = uiAnalysis.HasUITests || !uiAnalysis.RequiresUITesting;

            if (uiAnalysis.RequiresUITesting && !uiAnalysis.HasUITests)
            {
                result.Issues.Add("UI testing required but no Playwright/UI tests found");
                _logger.LogWarning("UI tests required but missing for feature: {Feature}", featureDescription);
            }

            // Determine overall pass/fail
            result.Passed = result.CompilationPassed && result.UnitTestsPassed && 
                           result.FunctionalTestsPassed && result.UITestsPassed;

            if (result.Passed)
            {
                result.Feedback = $"All validations passed for {featureDescription}. Code is ready for review.";
                _logger.LogInformation("Validation PASSED for feature: {Feature}", featureDescription);
            }
            else
            {
                result.Feedback = $"Validation FAILED for {featureDescription}. Issues found: {string.Join("; ", result.Issues)}";
                _logger.LogWarning("Validation FAILED for feature: {Feature}. Issues: {Issues}", featureDescription, string.Join("; ", result.Issues));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during code validation");
            result.Passed = false;
            result.Feedback = $"Validation error: {ex.Message}";
            result.Issues.Add($"Unexpected error during validation: {ex.Message}");
            return result;
        }
    }

    private async Task<bool> CheckCompilationAsync(string code)
    {
        try
        {
            // Check for obvious syntax errors in C# code
            var syntaxIssues = new List<string>();

            // Basic checks
            if (!code.Contains("namespace") && !code.Contains("class") && !code.Contains("async"))
            {
                syntaxIssues.Add("Code does not appear to be valid C#");
            }

            // Check for unmatched braces
            var openBraces = code.Count(c => c == '{');
            var closeBraces = code.Count(c => c == '}');
            if (openBraces != closeBraces)
            {
                syntaxIssues.Add("Mismatched braces in code");
            }

            // Check for unmatched parentheses
            var openParens = code.Count(c => c == '(');
            var closeParens = code.Count(c => c == ')');
            if (openParens != closeParens)
            {
                syntaxIssues.Add("Mismatched parentheses in code");
            }

            if (syntaxIssues.Count > 0)
            {
                _logger.LogWarning("Syntax issues detected: {Issues}", string.Join("; ", syntaxIssues));
                return false;
            }

            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking compilation");
            return false;
        }
    }

    private static TestCoverageAnalysis AnalyzeTestCoverage(string code)
    {
        var analysis = new TestCoverageAnalysis();

        // Check if test framework is used
        analysis.HasTests = Regex.IsMatch(code, TestFrameworkPattern, RegexOptions.IgnoreCase);

        if (!analysis.HasTests)
        {
            analysis.MissingScenarios.Add("No test framework detected (xUnit, NUnit, MSTest)");
            return analysis;
        }

        // Check for common test scenarios
        var hasValidInputTest = Regex.IsMatch(code, @"(ValidInput|ValidUser|HappyPath|Success)", RegexOptions.IgnoreCase);
        var hasInvalidInputTest = Regex.IsMatch(code, @"(InvalidInput|InvalidUser|ErrorCase|Invalid)", RegexOptions.IgnoreCase);
        var hasEdgeCaseTest = Regex.IsMatch(code, @"(EdgeCase|Boundary|Null|Empty|NullOrEmpty)", RegexOptions.IgnoreCase);
        var hasMaliciousInputTest = Regex.IsMatch(code, @"(Malicious|SqlInjection|XSS|Security|Attack)", RegexOptions.IgnoreCase);

        if (!hasValidInputTest)
            analysis.MissingScenarios.Add("No valid input/happy path tests detected");

        if (!hasInvalidInputTest)
            analysis.MissingScenarios.Add("No invalid input error handling tests detected");

        if (!hasEdgeCaseTest)
            analysis.MissingScenarios.Add("No edge case/boundary tests detected");

        if (!hasMaliciousInputTest)
            analysis.MissingScenarios.Add("No security/malicious input tests detected");

        // Check for mocking/faking
        var hasMocking = Regex.IsMatch(code, MockingFrameworkPattern, RegexOptions.IgnoreCase);
        if (!hasMocking)
            analysis.MissingScenarios.Add("No mocking framework detected for unit test isolation");

        return analysis;
    }

    private FunctionalScenarioAnalysis AnalyzeFunctionalScenarios(string code, string featureDescription)
    {
        var analysis = new FunctionalScenarioAnalysis();

        // Determine what functional scenarios are required based on feature description
        var featureUpper = featureDescription.ToUpperInvariant();

        if (featureUpper.Contains("LOGIN") || featureUpper.Contains("AUTHENTICATION"))
        {
            // Login feature requires: valid user, invalid user, malicious attempts
            var hasValidUserTest = Regex.IsMatch(code, @"(ValidUser|ValidCredentials|CorrectPassword|AuthenticationSuccess)", RegexOptions.IgnoreCase);
            var hasInvalidUserTest = Regex.IsMatch(code, @"(InvalidUser|WrongPassword|AuthenticationFailed|FailedLogin)", RegexOptions.IgnoreCase);
            var hasMaliciousTest = Regex.IsMatch(code, @"(SqlInjection|BruteFoce|DictionaryAttack|Malicious)", RegexOptions.IgnoreCase);

            if (!hasValidUserTest)
                analysis.MissingScenarios.Add("Login feature missing: valid user authentication test");
            if (!hasInvalidUserTest)
                analysis.MissingScenarios.Add("Login feature missing: invalid credentials test");
            if (!hasMaliciousTest)
                analysis.MissingScenarios.Add("Login feature missing: security attack prevention test");
        }

        if (featureUpper.Contains("CREATE") || featureUpper.Contains("INSERT") || featureUpper.Contains("ADD"))
        {
            // Create/Insert feature requires: valid creation, duplicate handling, validation
            var hasValidCreationTest = Regex.IsMatch(code, @"(Create|Insert|Add).*Success", RegexOptions.IgnoreCase);
            var hasDuplicateTest = Regex.IsMatch(code, @"(Duplicate|AlreadyExists|Conflict)", RegexOptions.IgnoreCase);
            var hasValidationTest = Regex.IsMatch(code, @"(Validate|Validation|Invalid)", RegexOptions.IgnoreCase);

            if (!hasValidCreationTest)
                analysis.MissingScenarios.Add("Create feature missing: successful creation test");
            if (!hasDuplicateTest)
                analysis.MissingScenarios.Add("Create feature missing: duplicate handling test");
            if (!hasValidationTest)
                analysis.MissingScenarios.Add("Create feature missing: input validation test");
        }

        if (featureUpper.Contains("DELETE") || featureUpper.Contains("REMOVE"))
        {
            // Delete feature requires: successful deletion, non-existent item, authorization
            var hasDeleteTest = Regex.IsMatch(code, @"(Delete|Remove).*Success", RegexOptions.IgnoreCase);
            var hasNotFoundTest = Regex.IsMatch(code, @"(NotFound|DoesNotExist|NotExists)", RegexOptions.IgnoreCase);
            var hasAuthTest = Regex.IsMatch(code, @"(Unauthorized|Permission|Forbidden)", RegexOptions.IgnoreCase);

            if (!hasDeleteTest)
                analysis.MissingScenarios.Add("Delete feature missing: successful deletion test");
            if (!hasNotFoundTest)
                analysis.MissingScenarios.Add("Delete feature missing: not found handling test");
            if (!hasAuthTest)
                analysis.MissingScenarios.Add("Delete feature missing: authorization test");
        }

        // Generic functional validation
        if (analysis.MissingScenarios.Count == 0)
        {
            analysis.HasRequiredScenarios = true;
        }

        return analysis;
    }

    private static UITestAnalysis AnalyzeUITestCoverage(string code, string featureDescription)
    {
        var analysis = new UITestAnalysis();

        // Determine if feature requires UI testing
        var featureUpper = featureDescription.ToUpperInvariant();
        analysis.RequiresUITesting = featureUpper.Contains("UI") || featureUpper.Contains("FRONTEND") || 
                                     featureUpper.Contains("PAGE") || featureUpper.Contains("FORM") ||
                                     featureUpper.Contains("LOGIN") || featureUpper.Contains("BUTTON");

        if (!analysis.RequiresUITesting)
        {
            // Backend features typically don't need UI tests
            return analysis;
        }

        // Check for Playwright or UI testing framework
        analysis.HasUITests = Regex.IsMatch(code, @"(Playwright|Selenium|UITest|E2E|EndToEnd)", RegexOptions.IgnoreCase);

        if (analysis.HasUITests)
        {
            // Verify different test scenarios
            var hasUserInteractionTest = Regex.IsMatch(code, @"(Click|Fill|Submit|Type)", RegexOptions.IgnoreCase);
            var hasNavigationTest = Regex.IsMatch(code, @"(Navigate|GoTo|Redirect)", RegexOptions.IgnoreCase);
            var hasValidationTest = Regex.IsMatch(code, @"(Assert|Expect|Verify)", RegexOptions.IgnoreCase);

            if (!hasUserInteractionTest)
                analysis.MissingScenarios.Add("UI tests missing: user interaction scenarios (click, fill, submit)");
            if (!hasNavigationTest)
                analysis.MissingScenarios.Add("UI tests missing: navigation scenarios");
            if (!hasValidationTest)
                analysis.MissingScenarios.Add("UI tests missing: assertion/validation scenarios");
        }

        return analysis;
    }

    private class TestCoverageAnalysis
    {
        public bool HasTests { get; set; }
        public List<string> MissingScenarios { get; set; } = new();
    }

    private class FunctionalScenarioAnalysis
    {
        public bool HasRequiredScenarios { get; set; }
        public List<string> MissingScenarios { get; set; } = new();
    }

    private class UITestAnalysis
    {
        public bool RequiresUITesting { get; set; }
        public bool HasUITests { get; set; }
        public List<string> MissingScenarios { get; set; } = new();
    }
}
