# MathAnalysisAI.Server Deployment Guide

This is the only deployment document for the repository.

For project architecture and development rules, use the root [`README.md`](../README.md).

## Scope

This guide covers:

- production-oriented Docker deployment
- required environment files
- database and LiteLLM container notes
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

## Required Environment Configuration

Recommended host-side env directory:

- `/etc/mathanalysis-ai/`

Typical files:

- `/etc/mathanalysis-ai/sqlserver.env`
- `/etc/mathanalysis-ai/litellm.env`
- `/etc/mathanalysis-ai/server.env`

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
LiteLLM__BaseUrl=http://mathanalysis-litellm:4000/v1/chat/completions
PhotoSolutionOcr__Provider=litellm
Auth__Mode=LocalPassword
ForwardedHeaders__KnownNetworks=172.16.0.0/12
```

Production safety rules:

- do not enable development fallback auth in production
- do not put upstream provider keys into frontend or repo-tracked files
- keep real secrets outside the repository

## Build and Start

From `MathAnalysisAI.Server/`:

```bash
docker compose -f docker-compose.prod.yml build
docker compose -f docker-compose.prod.yml up -d
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs -f server
```

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
- EF Core migrations run as part of startup flow
- database credentials in `server.env` must match `sqlserver.env`
- data volumes must be backed up before upgrades

## Reverse Proxy Notes

If Nginx or another reverse proxy is used:

- terminate TLS at the proxy
- forward `Host`, `X-Real-IP`, `X-Forwarded-For`, and `X-Forwarded-Proto`
- keep server/LiteLLM/SQL Server off direct public exposure where possible

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
