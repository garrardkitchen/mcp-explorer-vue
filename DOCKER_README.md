# MCP Explorer

A modern MCP (Model Context Protocol) server explorer — browse tools, prompts, resources, and chat with LLMs over live MCP connections. Built with a **Vue 3 / Vite / PrimeVue** frontend and an **ASP.NET Core 10** backend using clean architecture.

📚 **[Full Documentation](https://mcp-explorer-x-docs.garrardkitchen.com/)** — user guide, feature reference, and configuration

---

## Images

| Image | Description |
|-------|-------------|
| `garrardkitchen/mcp-explorer-x` | **Single container** — gateway + API bundled; simplest way to run |
| `garrardkitchen/mcp-explorer-x-api` | API only — for `docker-compose` separate-services deployments |
| `garrardkitchen/mcp-explorer-x-frontend` | Frontend (nginx) only — for `docker-compose` separate-services deployments |

---

## Quick Start — Single Container

```bash
docker run -d \
  -p 8080:8080 \
  -v mcp-data:/data \
  -e MCP_DATA_PATH=/data \
  garrardkitchen/mcp-explorer-x:latest
```

Open **http://localhost:8080** in your browser.

### Mount your Azure credentials (optional — for Key Vault and Entra features)

```bash
docker run -d \
  -p 8080:8080 \
  -v mcp-data:/data \
  -v ~/.azure:/root/.azure:ro \
  -e AZURE_CONFIG_DIR=/root/.azure \
  garrardkitchen/mcp-explorer-x:latest
```

---

## Quick Start — Separate Services (docker-compose)

1. Copy the example env file and fill in your values:

```bash
curl -O https://raw.githubusercontent.com/garrardkitchen/mcp-explorer-x/main/.env.example
cp .env.example .env
```

2. Create a `docker-compose.yml`:

```yaml
name: mcp-explorer

services:
  api:
    image: garrardkitchen/mcp-explorer-x-api:latest
    restart: unless-stopped
    env_file: .env
    environment:
      - ASPNETCORE_URLS=http://+:5000
      - ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT:-Production}
      - PREFERENCES__StoragePath=${PREFERENCES__StoragePath:-/data/settings.json}
      - AZURE_CONFIG_DIR=/root/.azure
    volumes:
      - ${MCP_DATA_PATH:-mcp-data}:/data
      - ${HOST_AZURE_CONFIG_DIR:-azure-config-empty}:/root/.azure:ro
    healthcheck:
      test: ["CMD", "curl", "-sf", "http://localhost:5000/healthz"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s
    networks:
      - internal

  frontend:
    image: garrardkitchen/mcp-explorer-x-frontend:latest
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
  azure-config-empty:

networks:
  internal:
    driver: bridge
```

3. Start:

```bash
docker compose up -d
```

Open **http://localhost:8090** in your browser.

---

## Features

- 🔌 **MCP Connections** — Streamable HTTP connections with custom headers, OAuth, and Azure Client Credentials authentication
- 🛠️ **Tools** — Browse and invoke tools with dynamic parameter forms; inspect JSON responses inline
- 💬 **Prompts** — List, execute, and pipe results directly to an LLM
- 📄 **Resources & Templates** — Browse MCP resources and expand templates with runtime parameters
- 🤖 **Chat** — SSE-streamed AI chat with automatic MCP tool calling; shows token usage and active tool badges
- ⚡ **Workflows** — Multi-step tool chain builder with execution history and built-in load testing
- 🛡️ **Sensitive Data Protection** — Regex + heuristic secret detection; AES-256 encryption at rest
- 🔐 **Azure Integration** — Browse Key Vaults for secrets, pick Entra app registrations, auto-populate tenant/scope from `az login`
- 🎨 **10 Themes** — Command Dark/Light, Nord, Dracula, Catppuccin Mocha, Solarized Light, GitHub Dark/Light, Material Dark/Light — persisted per user
- ⌨️ **Command Palette** — Ctrl+K / ⌘+K for keyboard-first navigation

---

## Environment Variables

### API / Runtime

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Runtime environment — controls logging verbosity and error detail. Values: `Development` \| `Staging` \| `Production` |
| `ASPNETCORE_URLS` | `http://+:5000` | URL(s) the API listens on inside the container. Do not change unless you also change the port mapping. |
| `AppMetadata__Version` | `0.5.0` | Version string returned by `GET /api/v1/info` and shown in the UI footer. |
| `MCP_CLIENT_NAME` | `mcp-explorer` | Client name sent to MCP servers during the protocol handshake and included in the `User-Agent` header. |

### Preferences / Storage

| Variable | Default | Description |
|----------|---------|-------------|
| `MCP_DATA_PATH` | *(named volume)* | Host directory containing `settings.json` (connections, workflows, models). Mount this to persist data across restarts. macOS: `/Users/<you>/Library/Application Support/McpExplorer` |
| `PREFERENCES__StoragePath` | `/data/settings.json` | Path to `settings.json` *inside* the container. Only change if you customise the volume target. |

### Azure (optional — enables Key Vault & App Registration features)

| Variable | Default | Description |
|----------|---------|-------------|
| `HOST_AZURE_CONFIG_DIR` | *(empty)* | Absolute path to your host `~/.azure` directory. Mounted into the container so the API can use your existing `az login` session. Leave empty to skip — Azure features show "not connected" but everything else still works. |
| `AZURE_CONFIG_DIR` | `/root/.azure` | Path to the `.azure` directory *inside* the container. Set automatically when `HOST_AZURE_CONFIG_DIR` is provided — do not change. |

### LLM Service

| Variable | Default | Description |
|----------|---------|-------------|
| `LlmService__OpenAiBaseUrl` | `https://api.openai.com/v1` | Base URL for OpenAI-compatible APIs (OpenAI, Azure OpenAI, local proxies). |
| `LlmService__AzureApiVersion` | `2024-02-15-preview` | Azure OpenAI REST API version used in the `?api-version=` query parameter. |
| `LlmService__MaxRetryAttempts` | `3` | Maximum retry attempts for a failed LLM request. |
| `LlmService__TimeoutSeconds` | `30` | Seconds to wait for an LLM response before timing out. |

### Tool Invocation

| Variable | Default | Description |
|----------|---------|-------------|
| `ToolInvoke__TimeoutSeconds` | `300` | Seconds to wait for a single MCP tool call to complete. Increase for slow or long-running tools. `0` = no timeout. |
| `ToolInvoke__MaxRetryAttempts` | `2` | Maximum automatic retry attempts when a tool call fails due to a dropped connection. |

### Elicitation

| Variable | Default | Description |
|----------|---------|-------------|
| `Elicitation__TimeoutSeconds` | `0` | Seconds to wait for a user to respond to an elicitation dialog. `0` = wait indefinitely (recommended for interactive use). |

### Gateway / docker-compose only

| Variable | Default | Description |
|----------|---------|-------------|
| `GATEWAY_PORT` | `8090` | Host port the frontend is exposed on. Access the app at `http://localhost:<GATEWAY_PORT>`. |
| `ReverseProxy__Clusters__api-cluster__Destinations__api__Address` | `http://api:5000/` | YARP cluster destination. Routes `/api/*` to the API service over the internal Docker network. Only needed in multi-container deployments. |

### Frontend (Vue / Vite) — build-time only

> These are baked into the static JS bundle at image build time and **cannot be changed at runtime**.

| Variable | Default | Description |
|----------|---------|-------------|
| `VITE_API_BASE_URL` | *(empty)* | Base URL the browser uses to reach the API. Empty = same origin through the gateway (recommended). Override only when the API is on a different origin, e.g. `http://localhost:5000`. |
| `VITE_APP_VERSION` | `0.5.0` | App version displayed in the UI footer. |

---

## Source & Licence

- **Source**: [github.com/garrardkitchen/mcp-explorer-x](https://github.com/garrardkitchen/mcp-explorer-x)
- **Docs**: [mcp-explorer-x-docs.garrardkitchen.com](https://mcp-explorer-x-docs.garrardkitchen.com/)
