---
title: "Quick Start"
description: "Run MCP Explorer with Docker in under 2 minutes."
weight: 1
---

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) 20.10+ installed and running
- A terminal / command prompt

---

## Option 1 — Single Container (`docker run`)

The fastest way to get started. One command, one container, no compose file needed. Your settings are persisted in a host directory so they survive container restarts and upgrades.

{{< tabs tabTotal="3" >}}

{{% tab tabName="macOS" %}}

```bash
dataRoot="$HOME/Library/Application Support/McpExplorerX-docker"
mkdir -p "${dataRoot}"

docker run --rm -it \
  -p 8091:8080 \
  -v "${dataRoot}:/root/.local/share/McpExplorer" \
  -v ~/.azure:/root/.azure \
  -e AZURE_CONFIG_DIR=/root/.azure \
  -e HOST_AZURE_CONFIG_DIR=${HOME}/.azure \
  -e PREFERENCES__StoragePath=/data/settings.json \
  -e ASPNETCORE_ENVIRONMENT=Production \
  garrardkitchen/mcp-explorer-x:latest

open -a "Google Chrome" "http://localhost:8091"
```

{{% /tab %}}

{{% tab tabName="Ubuntu" %}}

```bash
dataRoot="$HOME/.config/McpExplorerX-docker"
mkdir -p "${dataRoot}"

docker run --rm -it \
  -p 8091:8080 \
  -v "${dataRoot}:/root/.local/share/McpExplorer" \
  -v ~/.azure:/root/.azure \
  -e AZURE_CONFIG_DIR=/root/.azure \
  -e HOST_AZURE_CONFIG_DIR=${HOME}/.azure \
  -e PREFERENCES__StoragePath=/data/settings.json \
  -e ASPNETCORE_ENVIRONMENT=Production \
  garrardkitchen/mcp-explorer-x:latest

xdg-open "http://localhost:8091"
```

{{% /tab %}}

{{% tab tabName="Windows" %}}

```powershell
$dataRoot="$HOME\AppData\Local\McpExplorerX-docker"
New-Item -ItemType Directory -Force -Path $dataRoot | Out-Null

docker run --rm -it `
  -p 8091:8080 `
  -v "${dataRoot}:/root/.local/share/McpExplorer" `
  -v ~/.azure:/root/.azure `
  -e AZURE_CONFIG_DIR=/root/.azure `
  -e HOST_AZURE_CONFIG_DIR=${HOME}/.azure `
  -e PREFERENCES__StoragePath=/data/settings.json `
  -e ASPNETCORE_ENVIRONMENT=Production `
  garrardkitchen/mcp-explorer-x:latest

Start-Process "http://localhost:8091"
```

{{% /tab %}}

{{< /tabs >}}

| Flag | Purpose |
|------|---------|
| `--rm -it` | Automatically remove the container when it stops; attach an interactive terminal |
| `-p 8091:8080` | Expose the app on host port **8091** (container listens on 8080) |
| `-v <dataRoot>:/root/.local/share/McpExplorer` | Persist connections and settings across restarts |
| `-v ~/.azure:/root/.azure` | Mount your host Azure CLI config so `AzureCliCredential` can authenticate |
| `-e AZURE_CONFIG_DIR=/root/.azure` | Tell the Azure SDK where to find the CLI config inside the container |
| `-e HOST_AZURE_CONFIG_DIR=...` | Used by docker-compose to conditionally mount your `.azure` directory |
| `-e PREFERENCES__StoragePath=/data/settings.json` | Override the settings path to match the `/data` volume mount |
| `-e ASPNETCORE_ENVIRONMENT=Production` | Set the runtime environment (controls logging verbosity and error detail) |

---

## Option 2 — Docker Compose (separate services)

Better for long-running or team environments. Runs the API and frontend as separate services on an internal Docker network. All configuration is driven by a `.env` file.

**1. Grab the compose file and env template:**

{{< tabs tabTotal="3" >}}

{{% tab tabName="macOS" %}}

```bash
curl -fsSL -o docker-compose.yml \
  https://raw.githubusercontent.com/garrardkitchen/mcp-explorer-vue/main/docker-compose.yml

curl -fsSL -o .env \
  https://raw.githubusercontent.com/garrardkitchen/mcp-explore-vue/main/.env.example
```

{{% /tab %}}

{{% tab tabName="Ubuntu" %}}

```bash
curl -fsSL -o docker-compose.yml \
  https://raw.githubusercontent.com/garrardkitchen/mcp-explorer-vue/main/docker-compose.yml

curl -fsSL -o .env \
  https://raw.githubusercontent.com/garrardkitchen/mcp-explore-vue/main/.env.example
```

{{% /tab %}}

{{% tab tabName="Windows" %}}

```powershell
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/garrardkitchen/mcp-explorer-vue/main/docker-compose.yml" `
  -OutFile "docker-compose.yml"

Invoke-WebRequest -Uri "https://raw.githubusercontent.com/garrardkitchen/mcp-explorer-vue/main/.env.example" `
  -OutFile ".env"
```

{{% /tab %}}

{{< /tabs >}}

**2. Edit `.env` — set your data path and any overrides:**

```bash
# Point to your existing McpExplorer settings folder (optional — persists connections)
# macOS:   MCP_DATA_PATH=/Users/<you>/Library/Application Support/McpExplorerX
# Linux:   MCP_DATA_PATH=/home/<you>/.config/McpExplorerX
# Windows: MCP_DATA_PATH=C:\Users\<you>\AppData\Local\McpExplorerX
MCP_DATA_PATH=

# Host port the app is exposed on (default: 8090)
GATEWAY_PORT=8090
```

See the [Environment Variables](../../reference/environment-variables/) reference for the full list.

**3. Start the services:**

```bash
docker compose up -d
```

Open **http://localhost:8090** in your browser (or whichever port you set for `GATEWAY_PORT`).

---

### `docker-compose.yml`

```yaml
name: mcp-explorer

# ─── Separate-services deployment ─────────────────────────────────────────────
# Usage:
#   cp .env.example .env        # fill in your values
#   docker compose up -d
#
# Single-container alternative:
#   docker build -t mcp-explorer .
#   docker run -p 8090:8080 -v mcp-data:/root/.local/share/McpExplorer mcp-explorer
# ──────────────────────────────────────────────────────────────────────────────

services:

  api:
    build:
      context: .
      dockerfile: Dockerfile.api
    restart: unless-stopped
    env_file: .env
    environment:
      - ASPNETCORE_URLS=http://+:5000
      - ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT:-Production}
      - AppMetadata__Version=${AppMetadata__Version:-0.5.0}
      - LlmService__OpenAiBaseUrl=${LlmService__OpenAiBaseUrl:-https://api.openai.com/v1}
      - LlmService__MaxRetryAttempts=${LlmService__MaxRetryAttempts:-3}
      - LlmService__TimeoutSeconds=${LlmService__TimeoutSeconds:-30}
      - ToolInvoke__TimeoutSeconds=${ToolInvoke__TimeoutSeconds:-300}
      - ToolInvoke__MaxRetryAttempts=${ToolInvoke__MaxRetryAttempts:-2}
      - Elicitation__TimeoutSeconds=${Elicitation__TimeoutSeconds:-0}
      - MCP_CLIENT_NAME=${MCP_CLIENT_NAME:-mcp-explorer}
      - PREFERENCES__StoragePath=${PREFERENCES__StoragePath:-/data/settings.json}
      # Fixed inside the container — do not override
      - AZURE_CONFIG_DIR=/root/.azure
    volumes:
      - ${MCP_DATA_PATH:-mcp-data}:/data
      # Optional: mount your host ~/.azure so Azure Key Vault & app-registration
      # lookups work inside the container. Set HOST_AZURE_CONFIG_DIR in .env:
      #   macOS/Linux: HOST_AZURE_CONFIG_DIR=~/.azure
      #   Windows:     HOST_AZURE_CONFIG_DIR=%USERPROFILE%\.azure
      - ${HOST_AZURE_CONFIG_DIR:-azure-config-empty}:/root/.azure
    healthcheck:
      test: ["CMD", "curl", "-sf", "http://localhost:5000/healthz"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s
    extra_hosts:
      - "host.docker.internal:host-gateway"
    networks:
      - internal

  frontend:
    build:
      context: .
      dockerfile: Dockerfile.frontend
    restart: unless-stopped
    ports:
      - "${GATEWAY_PORT:-8090}:8080"
    depends_on:
      api:
        condition: service_healthy
    networks:
      - internal

volumes:
  mcp-data:
    driver: local
  # Empty fallback — used when HOST_AZURE_CONFIG_DIR is not set in .env
  azure-config-empty:
    driver: local

networks:
  internal:
    driver: bridge
```

---

## Option 3 — Single Container with All Variables (`docker run -e`)

Same single container as Option 1 but with every environment variable passed explicitly via `-e` flags. Useful when you cannot use a `.env` file or a volume mount — for example, in CI pipelines, cloud container services (Azure Container Instances, AWS ECS, etc.), or when scripting a fully-reproducible deployment.

{{< tabs tabTotal="3" >}}

{{% tab tabName="macOS / Linux" %}}

```bash
docker run --rm -it \
  -p 8090:8080 \
  \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://+:5000 \
  -e AppMetadata__Version=0.5.0 \
  \
  -e MCP_CLIENT_NAME=mcp-explorer \
  \
  -e PREFERENCES__StoragePath=/data/settings.json \
  -v "$HOME/Library/Application Support/McpExplorerX":/data \
  \
  -e LlmService__OpenAiBaseUrl=https://api.openai.com/v1 \
  -e LlmService__AzureApiVersion=2024-02-15-preview \
  -e LlmService__MaxRetryAttempts=3 \
  -e LlmService__TimeoutSeconds=30 \
  \
  -e ToolInvoke__TimeoutSeconds=300 \
  -e ToolInvoke__MaxRetryAttempts=2 \
  \
  -e Elicitation__TimeoutSeconds=0 \
  \
  garrardkitchen/mcp-explorer-x:latest
```

**With Azure Key Vault / App Registration support:**

```bash
docker run --rm -it \
  -p 8090:8080 \
  -v "$HOME/Library/Application Support/McpExplorerX":/data \
  -v "$HOME/.azure":/root/.azure \
  \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://+:5000 \
  -e AppMetadata__Version=0.5.0 \
  -e MCP_CLIENT_NAME=mcp-explorer \
  -e PREFERENCES__StoragePath=/data/settings.json \
  -e LlmService__OpenAiBaseUrl=https://api.openai.com/v1 \
  -e LlmService__AzureApiVersion=2024-02-15-preview \
  -e LlmService__MaxRetryAttempts=3 \
  -e LlmService__TimeoutSeconds=30 \
  -e ToolInvoke__TimeoutSeconds=300 \
  -e ToolInvoke__MaxRetryAttempts=2 \
  -e Elicitation__TimeoutSeconds=0 \
  -e AZURE_CONFIG_DIR=/root/.azure \
  \
  garrardkitchen/mcp-explorer-x:latest
```

{{% /tab %}}

{{% tab tabName="Windows (PowerShell)" %}}

```powershell
docker run --rm -it `
  -p 8090:8080 `
  `
  -e ASPNETCORE_ENVIRONMENT=Production `
  -e ASPNETCORE_URLS=http://+:5000 `
  -e AppMetadata__Version=0.5.0 `
  `
  -e MCP_CLIENT_NAME=mcp-explorer `
  `
  -e PREFERENCES__StoragePath=/data/settings.json `
  -v "$HOME\AppData\Local\McpExplorerX:/data" `
  `
  -e LlmService__OpenAiBaseUrl=https://api.openai.com/v1 `
  -e LlmService__AzureApiVersion=2024-02-15-preview `
  -e LlmService__MaxRetryAttempts=3 `
  -e LlmService__TimeoutSeconds=30 `
  `
  -e ToolInvoke__TimeoutSeconds=300 `
  -e ToolInvoke__MaxRetryAttempts=2 `
  `
  -e Elicitation__TimeoutSeconds=0 `
  `
  garrardkitchen/mcp-explorer-x:latest
```

**With Azure Key Vault / App Registration support:**

```powershell
docker run --rm -it `
  -p 8090:8080 `
  -v "$HOME\AppData\Local\McpExplorerX:/data" `
  -v "$HOME\.azure:/root/.azure" `
  `
  -e ASPNETCORE_ENVIRONMENT=Production `
  -e ASPNETCORE_URLS=http://+:5000 `
  -e AppMetadata__Version=0.5.0 `
  -e MCP_CLIENT_NAME=mcp-explorer `
  -e PREFERENCES__StoragePath=/data/settings.json `
  -e LlmService__OpenAiBaseUrl=https://api.openai.com/v1 `
  -e LlmService__AzureApiVersion=2024-02-15-preview `
  -e LlmService__MaxRetryAttempts=3 `
  -e LlmService__TimeoutSeconds=30 `
  -e ToolInvoke__TimeoutSeconds=300 `
  -e ToolInvoke__MaxRetryAttempts=2 `
  -e Elicitation__TimeoutSeconds=0 `
  -e AZURE_CONFIG_DIR=/root/.azure `
  `
  garrardkitchen/mcp-explorer-x:latest
```

{{% /tab %}}

{{< /tabs >}}

> **Note:** `VITE_API_BASE_URL` and `VITE_APP_VERSION` are **build-time** variables baked into the image — they cannot be changed via `-e` at runtime. The published image uses `VITE_API_BASE_URL=` (empty = same-origin, correct for the single-container setup).

See the [Environment Variables](../../reference/environment-variables/) reference for the full list.

---

## Verify It's Running

You should see the Connections page — MCP Explorer's home screen.

![Connections page — the landing screen after startup](/images/screenshots/home-connections.png)
*The Connections page is shown on startup. It lists all your saved MCP server connections.*

---

## Next Steps

- [Add your first connection](../../connections/managing-connections/) — connect to an MCP server
- [Explore Tools](../../tools/browsing-tools/) — browse and invoke MCP tools
- [Start chatting](../../chat/chat-overview/) — use an LLM with MCP tool calling
