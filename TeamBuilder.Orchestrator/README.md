# Orchestrator

This project serves as the Microsoft Foundry Hosted Agent framework that orchestrates collaboration between QA, PM, and Dev agents for feature development with comprehensive code validation and iterative feedback loops.

## Overview

This project implements a central orchestrator that:
- **Coordinates** between three specialized agents (QA, PM, Dev)
- **Validates** code quality through comprehensive checks (compilation, unit tests, functional tests, UI tests)
- **Implements iterative feedback loops** (up to 5 iterations) with Dev revising code based on QA feedback
- **Integrates with Ollama** for local LLM-powered agent reasoning
- **Logs** all interactions for observability and debugging

## Architecture

### Agents

#### QA Agent

- **Purpose**: Quality assurance and comprehensive code validation
- **Responsibilities**: 
  - Validate code compilation
  - Check unit test coverage (valid, invalid, edge case, security scenarios)
  - Verify functional tests cover required scenarios (login, create, delete, etc.)
  - Validate UI/Playwright tests when feature requires UI testing
- **Key Methods**: `ValidateFeatureAsync()` → returns `ValidationResult`

#### PM Agent

- **Purpose**: Product management and requirements definition
- **Responsibilities**: 
  - Define requirements from user requests
  - Validate feature requests
  - Provide context for development tasks
- **Key Methods**: `DefineRequirementsAsync()`, `ValidateFeatureRequestAsync()`

#### Dev Agent

- **Purpose**: Development and implementation with Ollama integration
- **Responsibilities**: 
  - Generate code from specifications
  - Implement features based on requirements
  - Revise code based on QA feedback
- **Key Methods**: `GenerateCodeAsync()`, `ImplementFeatureAsync()`, `ReviseCodeAsync()`

## Workflows

### Feature Development Workflow

Orchestrates feature development:
1. PM validates feature request
2. Dev generates code from specifications
3. QA validates implementation with comprehensive checks
4. If QA fails, Dev revises based on feedback (up to 5 iterations)
5. Loop continues until QA passes or max iterations reached

### Iterative Feedback Loop

Both workflows implement the same validation loop (`ValidateWithFeedbackLoopAsync`):
- **Iteration 1-4**: QA validates → if failed, Dev revises based on feedback
- **Iteration 5**: Final validation attempt; fails if QA does not pass
- **Early exit**: If QA passes on any iteration, workflow completes successfully

## Configuration

### Environment Setup

Create or update `appsettings.json` with Ollama configuration:
```json
{
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "Model": "neural-chat"
  }
}
```
## Dependencies

- `Microsoft.Agents.AI` (v1.5.0) - Core agent framework
- `Microsoft.Agents.AI.Foundry` (v1.5.0) - Foundry integration
- `OllamaSharp` (v5.5.2) - Ollama client for local LLM access

## Running Locally

### Prerequisites
1. .NET 10 SDK
2. Ollama running locally (`http://localhost:11434`)
3. A Ollama model installed (e.g., `neural-chat`, `llama2`)

### Start the Orchestrator
```bash
cd TeamBuilder.Orchestrator
dotnet run
```

Access at `http://localhost:5000`

### Example Request
```bash
curl -X POST http://localhost:5000/api/agent/feature-dev \
  -H "Content-Type: application/json" \
  -d '{"request": "Add login authentication with email and password"}'
```
