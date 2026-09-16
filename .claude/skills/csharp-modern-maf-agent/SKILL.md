---
name: csharp-modern-maf-agent
description: Generates and fixes ENTERPRISE-GRADE C# AI-agent code on the 2026 GA Microsoft Agent Framework (Microsoft.Agents.AI) + Microsoft.Extensions.AI standard — DI-first, Options-bound configuration, secure credentials (Managed Identity), resilience + OpenTelemetry middleware, keyed agents, injected tools via AIFunctionFactory. Not POC/top-level-script style.
license: MIT
compatibility: ".NET 10 (net10.0), Microsoft.Agents.AI 1.14.0+, Microsoft.Extensions.AI 10.5+"
---

# Modern Microsoft Agent Framework (MAF + MEAI) — Enterprise Skill

## When to Use
Use this skill **exclusively** when generating, fixing, or modifying C# code for AI agents. It
enforces the 2026 **GA** Microsoft Agent Framework (`Microsoft.Agents.AI`, v1.0 GA 2026-04-02) on
**Microsoft.Extensions.AI** (MEAI), and **forbids** legacy Semantic Kernel v1.x / AutoGen 0.2.

**Target: production enterprise applications, not POC scripts.** Emit code that is DI-first,
configuration-bound, observable, resilient, secure, testable, and cancellation-aware. Do **not**
copy the top-level-`Program.cs`, inline-`Environment.GetEnvironmentVariable`, hard-coded-endpoint,
raw-`ApiKeyCredential` style found in learning/sample projects — that is POC shorthand, not the
recommended shape. (Sample projects are still a valid source of *which API names compile* against a
given package version, just not of *structure or style*.)

Legacy → modern quick map (never emit the left column):
| Legacy (do NOT emit) | Modern enterprise equivalent |
|---|---|
| `Kernel`, `KernelBuilder`, `[KernelFunction]` | `AIAgent`, `IChatClient`, `[Description]` + `AIFunctionFactory.Create` |
| `ChatCompletionAgent`, `AzureAssistantAgent` (SK) | `chatClient.AsAIAgent(...)` registered via `builder.AddAIAgent(...)` |
| AutoGen `ConversableAgent`, `GroupChat` | `AIAgent` + `AgentSession` (+ MAF workflows for orchestration) |
| Hand-written JSON/YAML tool schemas | `AIFunctionFactory.Create(method)` (auto-reflects params) |
| `new HttpClient()` + manual retry loops | MEAI/`Microsoft.Extensions.Http.Resilience` middleware |
| Inline `Environment.GetEnvironmentVariable` | `IOptions<T>` bound from `IConfiguration` |
| API keys in code/env | Managed Identity; secrets only from Key Vault / secret store |

## Enterprise Principles (guardrails)

1. **DI-first, no top-level scripts.** Register the `IChatClient` and every `AIAgent` in the DI
   container (`AddChatClient` / `AddKeyedChatClient`, `AddAIAgent`). Resolve them by injection.
   Business logic lives in services/handlers, never in a `Main` body.

2. **Configuration via Options, never inline env reads.** Bind a strongly-typed options record from
   `IConfiguration` (appsettings + environment + secret store) and validate it on start
   (`ValidateDataAnnotations().ValidateOnStart()`). Fail fast on missing config.

3. **Secure credentials.** Default to **`ManagedIdentityCredential`** (Azure OpenAI / Foundry) in
   production; `DefaultAzureCredential` only for local dev. If a provider requires an API key (e.g.
   an OpenAI-compatible corporate gateway), pull it from Key Vault / user-secrets — **never** commit
   it and never hard-code an endpoint.

4. **Reflection-based tools only.** Wrap every tool through `AIFunctionFactory.Create(...)` and
   annotate the method + each parameter with `[Description(...)]`. Tools that need state are DI
   services injected into an agent factory — not statics.

5. **Observability + resilience are mandatory, not optional.** Every chat pipeline gets
   `.UseFunctionInvocation()`, `.UseOpenTelemetry(...)` (with `EnableSensitiveData` **off** in prod),
   `.UseLogging(...)`, and a resilience/retry layer. Add caching where responses are cacheable.

6. **Async + cancellation end-to-end.** Every method is `async`, returns `Task`/`IAsyncEnumerable`,
   and threads a `CancellationToken`. Use collection expressions `[...]` and `net10.0` idioms.

## Enterprise Bootstrap

### Options + validation
```csharp
public sealed class AgentOptions
{
    public const string SectionName = "Agent";
    [Required] public required Uri Endpoint { get; init; }     // e.g. Azure OpenAI or gateway URL
    [Required] public required string Model { get; init; }     // deployment / model id
    public string? ApiKey { get; init; }                       // null when using Managed Identity
}

builder.Services
    .AddOptions<AgentOptions>()
    .BindConfiguration(AgentOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

### Register the IChatClient pipeline in DI
```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using System.ClientModel;

builder.Services.AddDistributedMemoryCache();   // swap for Redis in production

builder.Services.AddChatClient(sp =>
{
    var o = sp.GetRequiredService<IOptions<AgentOptions>>().Value;

    // PRIMARY (recommended): Azure OpenAI with Managed Identity — no secrets.
    IChatClient inner = new AzureOpenAIClient(o.Endpoint, new ManagedIdentityCredential())
        .GetChatClient(o.Model)
        .AsIChatClient();

    // ALTERNATIVE: OpenAI-compatible corporate gateway that requires a key.
    // Pull ApiKey from Key Vault / user-secrets via config — never hard-code it.
    // IChatClient inner = new OpenAIClient(
    //         new ApiKeyCredential(o.ApiKey!),
    //         new OpenAIClientOptions { Endpoint = o.Endpoint })
    //     .GetChatClient(o.Model)
    //     .AsIChatClient();

    return inner.AsBuilder()
        .UseDistributedCache()          // cache identical requests
        .UseFunctionInvocation()        // automatic tool call/response loop
        .UseOpenTelemetry(configure: c => c.EnableSensitiveData = false)  // OFF in prod
        .UseLogging()
        .Build(sp);
});
```
> **Middleware order matters.** Outer layers wrap inner ones. Telemetry outermost traces the whole
> call including retries and tool loops; caching inside telemetry means cache hits are still traced.
> Adjust deliberately, not by accident.

### Resilience
Layer retry/rate-limit as a delegating `IChatClient` in the pipeline (custom `Use(...)` handler with
a `System.Threading.RateLimiting` limiter or a Polly-backed retry), **or** attach a
`Microsoft.Extensions.Http.Resilience` `AddStandardResilienceHandler()` to the provider's underlying
`HttpClient` where the app owns transport construction. Prefer transient-fault retries with jittered
backoff; never an unbounded retry loop.

### Register agents (keyed) with a factory + injected tools
```csharp
builder.Services.AddSingleton<KnowledgeBaseTool>();   // stateful tool as a DI service

builder.AddAIAgent("KnowledgeBase", (sp, name) =>
{
    var chat = sp.GetRequiredService<IChatClient>();
    var tool = sp.GetRequiredService<KnowledgeBaseTool>();
    return chat.AsAIAgent(new ChatClientAgentOptions
    {
        Name = name,
        ChatOptions = new ChatOptions
        {
            Instructions = "Answer strictly from the knowledge base; call the tool when unsure.",
            Tools = [AIFunctionFactory.Create(tool.QueryAsync)]
        }
    });
});
```

### Resolve and use (ASP.NET Core minimal API)
```csharp
app.MapPost("/api/chat", async (
    ChatRequest req,
    [FromKeyedServices("KnowledgeBase")] AIAgent agent,
    CancellationToken ct) =>
{
    var result = await agent.RunAsync<ChatResponse>(req.Message, cancellationToken: ct);
    return result.Result.OutOfContext
        ? Results.BadRequest(new { error = "Out of scope" })
        : Results.Ok(new { response = result.Result.Answer });
});
```

## Recipes

### Tool definition (DI service, not static)
```csharp
public sealed class KnowledgeBaseTool(IVectorStore store, ILogger<KnowledgeBaseTool> logger)
{
    [Description("Searches the enterprise knowledge base for relevant passages.")]
    public async Task<string> QueryAsync(
        [Description("The natural-language question to search for.")] string question,
        CancellationToken ct = default)
    {
        logger.LogInformation("KB query: {Question}", question);
        return await store.SearchAsync(question, ct);
    }
}
```

### Streaming
```csharp
await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(input, session, ct))
    await response.WriteAsync(update.Text, ct);
```

### Multi-turn state
- **Client-managed history** (stateless service): keep a `List<ChatMessage>` and `AddMessages(response)` each turn.
- **Service-managed conversation** (stateful backend): set `ChatOptions.ConversationId` from `response.ConversationId`.
- **Agent session** (MAF): `AgentSession session = await agent.CreateSessionAsync(ct);` then pass `session` to every `RunAsync`/`RunStreamingAsync`. Persist/rehydrate the session for durable conversations.
- **Long conversations**: cap context with a chat reducer (`SummarizingChatReducer` / `MessageCountingChatReducer`) in the pipeline.

### Structured output
```csharp
var result = await agent.RunAsync<MeetingDetails>(input, session, ct);
MeetingDetails details = result.Result;

public record MeetingDetails(
    [property: Description("Meeting title; empty if out of context.")] string Title,
    [property: Description("Start time; DateTime.MinValue if out of context.")] DateTime StartTime,
    [property: Description("Participants; empty array if out of context.")] string[] Participants,
    [property: Description("True if the request is NOT about meetings.")] bool OutOfContext);
```

### Human-in-the-loop approval (sensitive tools)
```csharp
AIFunction guarded = new ApprovalRequiredAIFunction(AIFunctionFactory.Create(tools.ScheduleMeeting));
// ...register on the agent's ChatOptions.Tools...

var response = await agent.RunAsync(input, session, ct);
var approvals = response.Messages.SelectMany(m => m.Contents)
    .OfType<ToolApprovalRequestContent>().ToList();
if (approvals.Count > 0)
{
    bool approved = await approvalService.RequestAsync(approvals[0], ct);   // real approval channel
    var reply = new ChatMessage(ChatRole.User, [approvals[0].CreateResponse(approved)]);
    response = await agent.RunAsync(reply, session, ct);
}
```

### MCP tools
Expose external capabilities via Model Context Protocol and register the resulting `AIFunction`s the
same way as local tools — prefer MCP over bespoke HTTP wrappers for cross-service tools.

## Security & Governance
- **Authn/z:** Microsoft Entra ID; agents run under least-privilege identities.
- **Content safety:** route inputs/outputs through Azure AI Content Safety for user-facing agents.
- **Secrets:** Key Vault / user-secrets only; managed identity preferred. Never log secrets; keep
  OpenTelemetry `EnableSensitiveData = false` in production.
- **Data:** honor tenant/data-residency; scrub PII before caching or tracing.

## Recommended Project Structure
```
src/
  MyAgent.Api/            // ASP.NET Core host: DI wiring, endpoints, telemetry exporters
  MyAgent.Agents/         // agent factories + ChatClientAgentOptions
  MyAgent.Tools/          // tool services (AIFunctionFactory targets)
  MyAgent.Core/           // options, contracts, records, domain
tests/
  MyAgent.Tests/          // mock IChatClient (see IChatClient impl example) for deterministic tests
```

## Packages (net10.0)
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Agents.AI" Version="1.14.0" />
  <PackageReference Include="Microsoft.Agents.AI.Hosting" Version="1.14.0" />   <!-- AddAIAgent, keyed agents -->
  <PackageReference Include="Microsoft.Extensions.AI" Version="10.5.1" />        <!-- ChatClientBuilder, Use* middleware -->
  <PackageReference Include="Azure.AI.OpenAI" Version="2.*" />                   <!-- AzureOpenAIClient -->
  <PackageReference Include="Azure.Identity" Version="1.*" />                    <!-- ManagedIdentityCredential -->
  <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.*" />
  <!-- For an OpenAI-compatible gateway instead of Azure OpenAI: -->
  <!-- <PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="1.14.0" /> -->
</ItemGroup>
```

## Self-check before returning code
- [ ] No top-level-script structure; `IChatClient` + agents registered in DI, resolved by injection.
- [ ] Config comes from bound, validated `IOptions<T>` — not inline `Environment.GetEnvironmentVariable`.
- [ ] Credentials: `ManagedIdentityCredential` (or dev `DefaultAzureCredential`); any key from a secret store, no hard-coded endpoint/key.
- [ ] Pipeline has `UseFunctionInvocation` + `UseOpenTelemetry` (sensitive data OFF) + `UseLogging` + resilience; caching where sensible.
- [ ] Tools via `AIFunctionFactory.Create` with `[Description]` on method + params; stateful tools are DI services.
- [ ] Async + `CancellationToken` throughout; collection expressions `[...]`; `net10.0`.
- [ ] No legacy `Kernel`/`ChatCompletionAgent`/AutoGen types.
