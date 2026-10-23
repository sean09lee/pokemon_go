# AI Advisor: Grounded Function Calling

## Summary

The **Professor Oak AI Advisor** (`TeamBuilder.AI`, `TeamBuilder.Services/AiTools`) is a Blazor-integrated
chat assistant built on Azure AI Foundry that does **not rely solely on the LLM's general knowledge**.
Instead, it uses OpenAI/Azure AI Foundry **function calling (tool use)** to invoke TeamBuilder's own
Pokémon GO services — live Hub rankings, PoGoAPI base stats, and type data — and grounds its answers in
that real, current data.

This demonstrates a core agentic AI engineering skill: **wiring a model up to call real backend
functions, deterministically, instead of trusting it to "know" the answer.**

## Why this matters

A plain chat completion call (`"user says X" -> "model replies Y"`) is indistinguishable from any
off-the-shelf chatbot demo. It also has a real correctness problem for this domain: Pokémon GO data
(move sets, CP, meta rankings, balance changes) changes over time and an LLM's training data can be
stale or simply wrong. By exposing the app's existing services as callable tools, the model is forced
to fetch ground-truth data before answering questions like:

- "What's the best fire-type attacker right now?"
- "What are Swampert's base stats?"
- "What type is Charizard?"

instead of hallucinating plausible-sounding but incorrect numbers.

## Architecture

```
AiAgentPanel.razor (Blazor UI)
		|
		v
IAIAdvisorService.SendMessageAsync(conversation)
		|
		v
AIAdvisorService  <-- orchestrates the tool-calling loop
   |         \
   |          \--> AiToolRegistry --> IAiTool implementations --> existing Services
   |                                                                (IHubApiService,
   v                                                                 IPogoApiService)
FoundryChatClient.CompleteAsync(messages, tools)
		|
		v
Azure AI Foundry chat deployment (Azure.AI.OpenAI / OpenAI.Chat SDK)
```

### Key pieces

| Component | Responsibility |
|---|---|
| `TeamBuilder.AI.FoundryChatClient` | Thin wrapper around the Azure AI Foundry `ChatClient`. Exposes `CompleteAsync(messages, tools)`, returning the raw `ChatCompletion` (including any requested tool calls) so the caller can decide what to do next. |
| `TeamBuilder.Services.AiTools.IAiTool` | Contract for a single callable tool: a name, a JSON-schema `ChatTool` definition, and an `InvokeAsync(argumentsJson)` method that executes real application logic. |
| `TeamBuilder.Services.AiTools.AiToolRegistry` | Aggregates all registered `IAiTool` instances (via DI), advertises their schemas to the model, and dispatches tool-call requests by function name. |
| `GetBestPokemonByTypeTool` | Calls `IHubApiService.CrawlBestPerTypeAsync` to return live, ranked attacker data from the Pokémon GO Hub tier list. |
| `GetPokemonBaseStatsTool` | Calls `IPogoApiService.GetPokemonStatsDataAsync` to return a Pokémon's real base Attack/Defense/Stamina. |
| `GetPokemonTypesTool` | Calls `IPogoApiService.GetPokemonTypesDataAsync` to return a Pokémon's real elemental type(s). |
| `AIAdvisorService` | Orchestrates the multi-round tool-calling loop (see below) and returns the final assistant message to the Blazor UI. |

### The tool-calling loop

1. Build the message list (system prompt + conversation history) and fetch the tool
   definitions from `AiToolRegistry`.
2. Call `FoundryChatClient.CompleteAsync(messages, tools)`.
3. If the model's `FinishReason` is **not** `ToolCalls`, return its text as the final answer.
4. Otherwise:
   - Append the assistant's tool-call request to the message history (required for the model
	 to see its own prior calls).
   - Execute each requested tool via `AiToolRegistry.ExecuteAsync(name, argumentsJson)`.
   - Append each tool's JSON result as a `ToolChatMessage` keyed by `ToolCallId`.
   - Loop back to step 2 (model may chain multiple tool calls, e.g. look up a Pokémon's type,
	 then look up counters).
5. A round cap (5) prevents runaway loops if the model gets stuck.

## Skills demonstrated

- **Function/tool-calling integration** with the OpenAI/Azure AI Foundry SDK (`ChatTool`,
  `ChatCompletionOptions.Tools`, `ChatFinishReason.ToolCalls`, `ToolChatMessage`).
- **JSON schema authoring** for tool parameters so the model knows what arguments to supply.
- **Clean separation of concerns**: the AI project (`TeamBuilder.AI`) knows nothing about
  Pokémon-specific tools; tools live in `TeamBuilder.Services` and are composed via DI, keeping
  the function-calling plumbing reusable for future tools.
- **Grounding / anti-hallucination strategy**: forcing the model to call real services rather
  than answer from memory, with structured JSON results instead of free text.
- **Multi-turn agentic control flow**: the service manages the request/response loop needed
  for tool calling, including safety bounds (max rounds) and error handling (malformed
  arguments, unknown tools, service failures all degrade gracefully into JSON error payloads).

## Extending this pattern

Adding a new capability is a 3-step pattern:

1. Implement `IAiTool` with a `ChatTool.CreateFunctionTool(...)` schema and an `InvokeAsync`
   that calls an existing (or new) service.
2. Register it in `ServiceRegistration.cs`: `services.AddScoped<IAiTool, YourNewTool>();`
3. No changes needed to `AIAdvisorService` or `FoundryChatClient` — the registry and loop pick
   it up automatically.

This same foundation is the natural base for the next steps in this project's AI roadmap:
multi-step agentic workflows (chaining several tool calls to accomplish a goal, e.g. "build me a
balanced Great League team") and automated evaluation (a prompt suite asserting which tools are
invoked and what the final answer should contain).
