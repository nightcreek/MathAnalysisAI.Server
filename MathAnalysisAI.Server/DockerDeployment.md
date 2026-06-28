# MathAnalysisAI.Server Deployment Guide

This is the only deployment document for the repository.

For project architecture and development rules, use the root [`README.md`](../README.md).

## Scope

This guide covers:

- production-oriented Docker deployment
- required environment files
- database and LiteLLM container notes
- frontend runtime and SSE deployment notes
- health checks
- reverse proxy notes
- update and rebuild flow

It does not redefine project architecture.

## Deployment Shape

The current deployment baseline uses:

- `sqlserver`
- `litellm`
- `server`

Primary files:

- `docker-compose.prod.yml`
- `Dockerfile`
- `server.env.example`

Deployment model:

- the repository provides templates and compose files only
- the deployment machine provides the real `server.env` and secret values
- no production secret is generated automatically from repository files
- `server.env.example` is a reference template only, not a production-ready file

## Required Environment Configuration

Recommended host-side env directory:

- `/etc/mathanalysis-ai/`

Typical files:

- `/etc/mathanalysis-ai/sqlserver.env`
- `/etc/mathanalysis-ai/litellm.env`
- `/etc/mathanalysis-ai/server.env`

Production environment setup:

1. copy `server.env.example` to a real `server.env` on the deployment machine
2. fill all required secrets manually
3. ensure the real `server.env` is never committed to git
4. start `docker compose` only after env files are complete and verified

### SQL Server

Example keys:

```env
ACCEPT_EULA=Y
MSSQL_SA_PASSWORD=YourStrong!Passw0rd
MSSQL_PID=Express
```

### LiteLLM

Example keys:

```env
DEEPSEEK_API_KEY=...
DASHSCOPE_API_KEY=...
LITELLM_MASTER_KEY=...
```

### Server

Key settings to verify:

```env
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Server=mathanalysis-sqlserver,1433;Database=MathAnalysisAI;User Id=sa;Password=...;TrustServerCertificate=True;MultipleActiveResultSets=true
LLMGateway__Mode=litellm
LiteLLM__ApiKey=...
LiteLLM__BaseUrl=http://mathanalysis-litellm:4000/v1/chat/completions
PhotoSolutionOcr__Provider=litellm
Auth__Mode=LocalPassword
ForwardedHeaders__KnownNetworks=172.16.0.0/12
```

Current runtime requirements:

- `LiteLLM__ApiKey` is required at runtime
- `LLMGateway` will fail if `LiteLLM__ApiKey` is missing
- the OCR provider also depends on the same LiteLLM key
- these values are deployment-time secrets only and must not be stored in repo-tracked files

Production safety rules:

- do not enable development fallback auth in production
- do not put upstream provider keys into frontend or repo-tracked files
- keep real secrets outside the repository
- do not use `server.env.example` as-is in production

## Build and Start

From `MathAnalysisAI.Server/`:

```bash
docker compose -f docker-compose.prod.yml build
docker compose -f docker-compose.prod.yml up -d
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs -f server
```

Port binding notes:

- the server process is bound by `ASPNETCORE_URLS`
- `docker-compose.prod.yml` is authoritative for runtime binding
- current internal container port is `8080`
- current host mapping example is `127.0.0.1:5131:8080`
- `Dockerfile EXPOSE` is informational only and does not override compose binding

## Health Checks

Current endpoints:

- `/health`
- `/ready`
- `/api/health`

Quick checks:

```bash
curl http://localhost:5131/health
curl http://localhost:5131/ready
curl http://localhost:5131/api/health
```

## Database Notes

- the application uses SQL Server
- EF Core migrations run automatically as part of startup flow
- startup also runs seed routines for prompt profiles and optional bootstrap users/admin data
- database credentials in `server.env` must match `sqlserver.env`
- data volumes must be backed up before upgrades
- the database user must have permission to apply pending migrations at startup

## Reverse Proxy Notes

If Nginx or another reverse proxy is used:

- terminate TLS at the proxy
- forward `Host`, `X-Real-IP`, `X-Forwarded-For`, and `X-Forwarded-Proto`
- keep server/LiteLLM/SQL Server off direct public exposure where possible
- analysis streaming uses Server-Sent Events (SSE)
- buffering must be disabled for the streaming analysis endpoint so the runtime UI receives incremental updates correctly

Current streaming endpoint:

- `POST /api/learning-analysis/analyze/stream`

## Update Flow

Typical update flow:

```bash
git pull
docker compose -f docker-compose.prod.yml build --no-cache
docker compose -f docker-compose.prod.yml up -d
docker compose -f docker-compose.prod.yml logs -f server
```

If env files changed:

```bash
docker compose -f docker-compose.prod.yml up -d --force-recreate
```

## Production Safety Notes

- back up database state before rebuilding or migrating
- do not expose SQL Server or LiteLLM directly to the public internet
- verify auth mode is production-safe before startup
- treat `docker compose config` output as sensitive because it can expand env-file values
