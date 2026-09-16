# develop-agents

A collection of small, focused .NET samples for building AI agents using the modern **Microsoft Agent Framework (Microsoft.Agents.AI)** and **Microsoft.Extensions.AI**.

This repo is organized as multiple independent console/apps and Aspire samples. Each folder is intended to be runnable on its own.

## Prerequisites

- .NET SDK (8.0 or later recommended)
- A supported AI provider configuration (for example Azure OpenAI or OpenAI)

Notes:
- Do not commit build outputs. This repo ignores `bin/` and `obj/` via `.gitignore`.
- You will typically provide model/provider settings via `appsettings.json`, environment variables, or user-secrets depending on the sample.

## Quickstart

Restore and build everything:

```powershell
# from the repo root
-dotnet restore
-dotnet build
```

Run a specific sample:

```powershell
# example
-dotnet run --project .\HelloAgent\HelloAgent.csproj
```

If a sample uses Aspire, run the AppHost project:

```powershell
-dotnet run --project .\AspireAgenticWorkFlow\AspireAgenticWorkFlow.AppHost\AspireAgenticWorkFlow.AppHost.csproj
```

## Solution

Open the solution file in Visual Studio / VS Code:

- `develop-agents.slnx`

## Samples

Console / app samples:

- `HelloAgent` - minimal “hello agent” sample
- `BasicAgentApp` - basic agent application
- `AgenticWorkflow` - agentic workflow orchestration sample
- `AgentResponseStreaming` - streaming responses sample
- `MultiTurnSessionAgent` - multi-turn conversation/session example
- `HumanInLoopAgent` - human-in-the-loop flow
- `StructuredOutput` - structured output / schemas
- `BasicTextRagExample` - basic text RAG example
- `CustomContextProvider` - custom context injection/provider example

Aspire samples:

- `AspireAgenticWorkFlow` - Aspire app host + services for agentic workflow
- `AspireAgentObservability` - Aspire-based observability sample

Vector store sample:

- `FueliXVectorStore` - Qdrant vector store example (`QdrantVectorStore.csproj`)

## Configuration

Most samples require configuring a model endpoint and credentials.

Typical approaches used across .NET samples:

- `appsettings.json` / `appsettings.Development.json`
- Environment variables
- .NET user-secrets (recommended for local development)

If you tell me which provider you want to use (Azure OpenAI vs OpenAI) I can add a concrete example config section that matches the projects in this repo.

## Conventions Used Here

- No legacy AutoGen (0.2) and no experimental Semantic Kernel v1 abstractions.
- Prefer **Microsoft.Agents.AI** (2026 GA) + **Microsoft.Extensions.AI**.
- Define tools via `AIFunctionFactory` so C# parameters are reflected automatically.

## Troubleshooting

- Build output tracked by git: ensure `.gitignore` includes `**/bin/` and `**/obj/` (it does in this repo).
- Git push fails with "repository not found": the GitHub repo must exist and you must be authenticated (Git Credential Manager is recommended).
