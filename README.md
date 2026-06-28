# MathAnalysisAI.Server

## Project Positioning

`MathAnalysisAI.Server` is the backend server for the Math Analysis AI project.

It provides API services for frontend clients, owns the analysis workflow, and serves the project's static frontend assets.

It is not:

- a frontend rendering engine
- a CAS engine
- an AST-based symbolic computation system

## Current Architecture Summary

The server is a modular monolith with interface-first and contract-first boundaries.

Current architectural baseline:

- controllers stay thin
- application services own business decisions
- intelligence logic is isolated into dedicated Intelligence layer modules
- persistence access is isolated behind narrow interfaces
- EF Core stays inside `Data/` and persistence adapters
- the analysis stack runs through a fixed pipeline
- the analysis frontend is bootstrapped through a runtime entry layer
- module boundaries are checked by the ModuleContracts validator
- contract drift is checked by contract validation and compilation tooling
- architecture drift is locked by the Freeze layer

## Canonical Request Flow

The canonical backend request flow is:

`Controller -> Application Service -> Module Interface -> Persistence Seam -> EF-backed Adapter -> Response DTO`

For analysis requests, the internal flow is:

`Controller -> IAnalysisService -> IAnalysisPipeline -> Ordered Analysis Steps -> Persistence / Response Mapping`

Frontend rendering uses response DTOs and MAMP-facing fields where applicable. Backend semantic reasoning does not depend on frontend rendering code.

For the analysis page, the frontend runtime flow is:

`analysis.html -> analysisBootstrap.js -> AnalysisUIRuntime -> Renderer / Motion / Perception`

## Analysis Pipeline

The analysis module is stabilized around these core types:

- `IAnalysisService`
- `IAnalysisPipeline`
- `AnalysisExecutionContext`

`AnalysisExecutionContext` is split conceptually into:

- `Input`
- `Runtime`
- `Output`

The fixed pipeline steps are:

- `UAOBuilderStep`
- `OCRStep`
- `LLMStep`
- `EvaluationStep`
- `PersistenceStep`

Pipeline rules:

- only the pipeline orchestrates steps
- steps do not call each other
- shared step state flows only through `AnalysisExecutionContext`
- controllers must not reference pipeline steps directly

## Core Contracts

### DTO contract

DTOs are HTTP transport contracts only.

### UAO semantic contract

UAO types are semantic analysis inputs and must stay independent from HTTP, EF Core, and frontend concerns.

### AnalysisResult domain contract

`AnalysisResult` and related domain models are backend semantic/domain outputs and must not be coupled to frontend rendering shapes.

### MAMP contract

MAMP is a frontend rendering protocol only. It does not define backend reasoning semantics.

### Mapping direction

Allowed mapping direction:

- `AnalysisRequestDto -> UAO`
- `UAO -> AnalysisResult`
- `AnalysisResult -> AnalysisResponseDto`

Forbidden direction:

- DTOs as internal semantic models across the service layer
- frontend rendering concerns flowing back into domain reasoning

## Module Boundaries

Current conceptual modules:

- Auth
- Analysis
- Course
- Admin
- Materials
- Knowledge
- Learning
- LLM
- OCR
- Persistence
- SharedKernel

Boundary rule:

- modules communicate through public interfaces and narrow seams
- modules must not depend on other modules' internal implementations

## Persistence Boundary

Persistence rules are strict:

- `ApplicationDbContext` and EF Core must remain inside `Data/` and persistence adapters
- application services must use narrow persistence-facing interfaces
- controllers must never use `DbContext` directly
- `IQueryable`, `DbSet<T>`, and EF tracking behavior must not leak upward

## Architecture Guard

The repository includes a lightweight ModuleContracts validator under `Architecture/ModuleContracts`.

Additional enforcement layers:

- `Architecture/ContractValidator`
- `Architecture/ContractCompiler`
- `Architecture/Freeze`

Modes:

- default mode: observation, non-blocking
- strict mode: blocks only high-confidence violations

Current expected result:

- `blocking = 0`
- `observation = 0`
- `acceptedLegacy = 0`

CI behavior:

- always generates the module contract report artifact
- runs strict mode as a blocking regression check

## Build and Test Commands

Main commands:

```bash
dotnet build MathAnalysisAI.Server/MathAnalysisAI.Server.csproj --configuration Release --no-restore
dotnet build Architecture/ModuleContracts/Architecture.ModuleContracts.csproj
dotnet build Architecture/ContractValidator/Architecture.ContractValidator.csproj
dotnet build Architecture/ContractCompiler/Architecture.ContractCompiler.csproj
dotnet build Architecture/Freeze/Architecture.Freeze.csproj
dotnet test MathAnalysisAI.Server/MathAnalysisAI.Server.sln --no-build
dotnet run --project Architecture/ModuleContracts/Architecture.ModuleContracts.csproj
dotnet run --project Architecture/ModuleContracts/Architecture.ModuleContracts.csproj -- --strict
dotnet run --project Architecture/ContractValidator/Architecture.ContractValidator.csproj -- Contracts/api.contract.json contract-mismatches.json
dotnet run --project Architecture/ContractCompiler/Architecture.ContractCompiler.csproj -- Contracts/api.contract.json MathAnalysisAI.Server/wwwroot/js/api/compiled-contracts.js
dotnet run --project Architecture/Freeze/Architecture.Freeze.csproj -- architecture-drift-report.json dependency-graph.json intelligence-isolation-report.json --strict
```

## Development Rules for Humans and AI Agents

- do not add features during architecture cleanup
- do not bypass module interfaces
- do not inject `DbContext` into controllers or application services
- do not let UAO depend on DTOs
- do not let domain types depend on ASP.NET or DTO namespaces
- do not reference analysis steps outside the pipeline
- do not introduce generic repository abstractions unless explicitly approved
- preserve API behavior unless a task explicitly allows changing it

## Deployment

Server deployment is documented in:

- [`MathAnalysisAI.Server/DockerDeployment.md`](MathAnalysisAI.Server/DockerDeployment.md)

That guide covers Docker, manual environment provisioning, health checks, SSE-aware reverse proxy notes, rebuild flow, and production safety notes.

## Current Stable Baseline

Current stable baseline:

- strict module boundary validation is green
- contract validation and contract compilation tooling are in place
- Freeze layer validation is in place
- accepted legacy count is zero
- server build is green
- full tests are green
- runtime behavior has been preserved through the architecture convergence line

## Tool-local Documentation

The following tool-local docs may remain because they explain local tooling rather than project-wide architecture:

- `Architecture/ModuleContracts/README.md`
- `MathAnalysisAI.Server/infra/litellm/README.md`
- `MathAnalysisAI.Server/Tools/Symbolic/README.md`

This root `README.md` is the only authoritative full project document.
