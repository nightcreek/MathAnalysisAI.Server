# ModuleContracts Validator

This folder contains the lightweight module boundary validator for `MathAnalysisAI.Server`.

For the full project architecture baseline, use the root [`README.md`](../../README.md).

## What it does

- reads `module-contracts.json`
- scans source files for deterministic boundary violations
- writes `module-contract-violations.json`

## Run locally

Observation mode:

```bash
dotnet run --project Architecture/ModuleContracts/Architecture.ModuleContracts.csproj
```

Strict mode:

```bash
dotnet run --project Architecture/ModuleContracts/Architecture.ModuleContracts.csproj -- --strict
```

## Modes

- observation mode always exits `0`
- strict mode exits non-zero only for blocking violations

Current expected project baseline:

- `blocking = 0`
- `observation = 0`
- `acceptedLegacy = 0`
