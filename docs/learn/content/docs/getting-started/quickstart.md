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
  -p 8090:8080 \
  -v "${dataRoot}:/root/.local/share/McpExplorer" \
  garrardkitchen/mcp-explorer-x:latest

open -a "Google Chrome" "http://localhost:8090"
```

{{% /tab %}}

{{% tab tabName="Ubuntu" %}}

```bash
dataRoot="$HOME/.config/McpExplorerX-docker"
mkdir -p "${dataRoot}"

docker run --rm -it \
  -p 8090:8080 \
  -v "${dataRoot}:/root/.local/share/McpExplorer" \
  garrardkitchen/mcp-explorer-x:latest

xdg-open "http://localhost:8090"
```

{{% /tab %}}

{{% tab tabName="Windows" %}}

```powershell
$dataRoot="$HOME\AppData\Local\McpExplorerX-docker"
New-Item -ItemType Directory -Force -Path $dataRoot | Out-Null

docker run --rm -it `
  -p 8090:8080 `
  -v "${dataRoot}:/root/.local/share/McpExplorer" `
  garrardkitchen/mcp-explorer-x:latest

Start-Process "http://localhost:8090"
```

{{% /tab %}}

{{< /tabs >}}

| Flag | Purpose |
|------|---------|
| `-p 8090:8080` | Expose the app on host port 8090 |
| `-v <dataRoot>:/root/.local/share/McpExplorer` | Persist connections and settings across restarts |
| `--rm` | Automatically remove the container when it stops |

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
    volumes:
      - ${MCP_DATA_PATH:-mcp-data}:/data
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

networks:
  internal:
    driver: bridge
```

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
