# LiteLLM Local Proxy

This directory contains the local LiteLLM proxy setup for `MathAnalysisAI.Server`.

For full project architecture and development rules, use the root [`README.md`](../../README.md).

## Purpose

- keep upstream model provider keys out of ASP.NET application settings
- expose stable model aliases to the server
- support local proxy startup through Python or Docker

## Local Files

- `config.yaml`: alias and provider routing
- `.env.example`: local environment variable template
- `docker-compose.litellm.yml`: Docker-based startup

## Stable Aliases

The backend should call aliases instead of provider-specific model names:

- `math-reviewer`
- `math-solver`
- `math-hint`
- `math-explainer`
- `photo-solution-ocr`
- `math-material-ocr`

## Required Environment Variables

Common local keys:

- `LITELLM_MASTER_KEY`
- `DEEPSEEK_API_KEY`
- `DASHSCOPE_API_KEY`
- `SILICONFLOW_API_KEY`
- `OPENROUTER_API_KEY`
- `OPENAI_API_KEY`
- `GEMINI_API_KEY`

The ASP.NET server should use only the LiteLLM endpoint and LiteLLM key, not upstream provider keys.

## Run Locally

Python:

```bash
pip install litellm
cd MathAnalysisAI.Server/infra/litellm
cp .env.example .env
export LITELLM_MASTER_KEY="sk-local-litellm-master-key"
export DEEPSEEK_API_KEY="your-real-key"
export DASHSCOPE_API_KEY="your-real-key"
litellm --config config.yaml --port 4000
```

Docker:

```bash
cd MathAnalysisAI.Server/infra/litellm
cp .env.example .env
docker compose -f docker-compose.litellm.yml up -d
```

## Server-side Settings

Typical server-side settings:

```env
LLMGateway__Mode=litellm
LiteLLM__ApiKey=sk-local-litellm-master-key
LiteLLM__BaseUrl=http://localhost:4000/v1/chat/completions
PhotoSolutionOcr__Provider=litellm
```

## Quick Check

```bash
curl -X POST http://localhost:4000/v1/chat/completions \
  -H "Authorization: Bearer sk-local-litellm-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "math-reviewer",
    "messages": [{"role": "user", "content": "Say ok."}]
  }'
```

## Local Caveats

- do not commit real keys
- restart LiteLLM after changing provider keys
- if auth still fails, check `.env`, shell exports, and deployed env files for placeholder values
- keep upstream provider keys in LiteLLM only, not in frontend code or repo-tracked server config
