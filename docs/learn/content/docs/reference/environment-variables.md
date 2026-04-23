---
title: "Environment Variables"
description: "Complete reference for all environment variables supported by MCP Explorer."
weight: 3
---

## Overview

MCP Explorer is configured entirely via environment variables — there are no required config files to edit. Copy `.env.example` to `.env`, fill in your values, and pass it to Docker or Docker Compose.

```bash
# Docker Compose (reads .env automatically)
cp .env.example .env
docker compose up -d

# docker run (pass vars individually)
docker run -p 8090:8080 \
  -e MCP_CLIENT_NAME=my-app \
  -e MCP_DATA_PATH=/data \
  -v /path/to/data:/data \
  ghcr.io/your-org/mcp-explorer:latest
```

> **info:** In Docker Compose, variables in `.env` are interpolated into `docker-compose.yml` automatically. Variables prefixed `VITE_` are **build-time only** — they are baked into the static JS bundle and cannot be changed at runtime.

---

## API Container Variables

These apply to the `api` service (`Garrard.Mcp.Explorer.Api`).

### ASP.NET Core

| Variable | Default | Description |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Runtime environment. `Development` enables detailed error pages and Swagger UI. `Production` suppresses stack traces. |
| `ASPNETCORE_URLS` | `http://+:5000` | The URL(s) the API listens on inside the container. Do not change unless you also remap the internal port. |

### App Metadata

| Variable | Default | Description |
|---|---|---|
| `AppMetadata__Version` | `0.5.0` | Version string returned by `GET /api/v1/info` and displayed in the UI footer. Set this to match your release tag. |

### MCP Client

| Variable | Default | Description |
|---|---|---|
| `MCP_CLIENT_NAME` | `mcp-explorer` | Client name sent to MCP servers during the protocol handshake. Also included in the `User-Agent` header as `<name>:<version>/<hostname>`. Override to brand requests from your instance. |

### Preferences / Storage

| Variable | Default | Description |
|---|---|---|
| `MCP_DATA_PATH` | *(docker volume)* | **Host** directory to mount as the data volume. Set this to your existing `McpExplorer` data directory to reuse saved connections and settings. When empty, Docker Compose creates a named volume called `mcp-data`. |
| `PREFERENCES__StoragePath` | `/data/settings.json` | Absolute path to `settings.json` **inside the container**. Only change this if you customise the volume mount target. |

**Default data directory locations on the host** (for `MCP_DATA_PATH`):

| Platform | Path |
|---|---|
| macOS | `/Users/<you>/Library/Application Support/McpExplorer` |
| Linux | `/home/<you>/.local/share/McpExplorer` |
| Windows | `C:\Users\<you>\AppData\Local\McpExplorer` |

### Dev Tunnels

`HOST_DEVTUNNELS_DIR` lets the container inherit your host machine's existing `devtunnel user login` session, so you never have to go through the device-code flow inside Docker.

| Variable | Default | Description |
|---|---|---|
| `HOST_DEVTUNNELS_DIR` | *(empty)* | **Host** path to the DevTunnels credentials directory. When set, Docker Compose mounts this into the container at `/root/.local/share/DevTunnels` so the `devtunnel` CLI reuses your existing login. Leave empty to fall back to a named Docker volume (`devtunnels-cli`) — credentials persist across `docker compose down` but you will still need to complete a one-time device-code login from the MCP Explorer UI. |

**Per-OS values for `HOST_DEVTUNNELS_DIR`:**

| Platform | Value |
|---|---|
| macOS | `HOST_DEVTUNNELS_DIR=/Users/<you>/Library/Application Support/DevTunnels` |
| Linux | `HOST_DEVTUNNELS_DIR=/home/<you>/.devtunnels` |
| Windows | `HOST_DEVTUNNELS_DIR=C:\Users\<you>\AppData\Local\DevTunnels` |

> **tip:** If you have never run `devtunnel user login` on your host, leave this empty and complete the one-time device-code login from the **Device Code Login** dialog in MCP Explorer. Credentials are written to the `devtunnels-cli` named volume and will survive `docker compose down` (but not `docker compose down -v`).

### Azure Integration

MCP Explorer can use Azure Key Vault secrets and Entra App Registrations to populate connection credentials. The API container uses `DefaultAzureCredential` (Azure CLI → environment → managed identity) to authenticate with Azure.

| Variable | Default | Description |
|---|---|---|
| `HOST_AZURE_CONFIG_DIR` | *(empty)* | **Host** path to your `.azure` directory (e.g. `~/.azure`). When set, Docker Compose mounts this directory into the container at `/root/.azure` so the Azure CLI credential (`az login` session) is available inside Docker. Leave empty to skip — Azure features show "not connected" but all other functionality continues. |
| `AZURE_CONFIG_DIR` | `/root/.azure` | Path to the `.azure` directory **inside the container**. Set automatically to `/root/.azure` in `docker-compose.yml` — do not override this unless you are customising the volume mount target. |

**Per-OS values for `HOST_AZURE_CONFIG_DIR`:**

| Platform | Value |
|---|---|
| macOS | `HOST_AZURE_CONFIG_DIR=~/.azure` |
| Linux | `HOST_AZURE_CONFIG_DIR=~/.azure` |
| Windows | `HOST_AZURE_CONFIG_DIR=%USERPROFILE%\.azure` |

> **tip:** The volume is mounted **read-write** (not read-only) because the Azure CLI refreshes expired access tokens by writing back to `msal_token_cache.json`. A read-only mount silently blocks token refresh and causes authentication failures.

### LLM Service

| Variable | Default | Description |
|---|---|---|
| `LlmService__OpenAiBaseUrl` | `https://api.openai.com/v1` | Base URL for OpenAI-compatible APIs. Override to point at Azure OpenAI, a local proxy (e.g. Ollama, LM Studio), or any OpenAI-compatible endpoint. |
| `LlmService__AzureApiVersion` | `2024-02-15-preview` | Azure OpenAI REST API version appended as `?api-version=`. Only relevant when using Azure OpenAI endpoints. |
| `LlmService__MaxRetryAttempts` | `3` | Maximum automatic retries for a failed LLM API request. |
| `LlmService__TimeoutSeconds` | `30` | Seconds to wait for an LLM response before timing out. Increase for large context windows or slow providers. |

### Tool Invocation

| Variable | Default | Description |
|---|---|---|
| `ToolInvoke__TimeoutSeconds` | `300` | Seconds to wait for a single MCP tool call to complete. Increase for slow or long-running tools. Set to `0` to disable the timeout entirely. |
| `ToolInvoke__MaxRetryAttempts` | `2` | Maximum automatic retries when a tool call fails due to a dropped MCP connection. |

### Elicitation

| Variable | Default | Description |
|---|---|---|
| `Elicitation__TimeoutSeconds` | `0` | Seconds to wait for a user to respond to an elicitation dialog. `0` = wait indefinitely (recommended for interactive use). Set a positive integer to auto-decline after the timeout. |

---

## Gateway / Frontend Container Variables

These apply to the `frontend` service (`Garrard.Mcp.Explorer.Gateway`) and control how the gateway routes traffic and what port is exposed.

### Port Mapping

| Variable | Default | Description |
|---|---|---|
| `GATEWAY_PORT` | `8090` | Host port the gateway is exposed on. Access the app at `http://localhost:<GATEWAY_PORT>`. Maps to container port `8080`. |

### YARP Reverse Proxy

| Variable | Default | Description |
|---|---|---|
| `ReverseProxy__Clusters__api-cluster__Destinations__api__Address` | `http://localhost:5000/` | The upstream address YARP uses to forward `/api/*` and `/oauth/*` requests. In single-container mode this points to `localhost:5000`. In `docker compose` it is overridden to `http://api:5000/` so the gateway reaches the `api` service over the internal Docker network. |

---

## Frontend Build-Time Variables (`VITE_*`)

> **warning:** These variables are **baked into the static JS bundle at build time**. They cannot be changed after the image is built. If you need different values, rebuild the image.

| Variable | Default | Description |
|---|---|---|
| `VITE_API_BASE_URL` | *(empty)* | Base URL the browser uses to reach the API. Empty string means same-origin — requests go through the gateway (recommended). Override only when the API is on a different origin, e.g. `http://localhost:5000`. |
| `VITE_APP_VERSION` | `0.5.0` | App version displayed in the UI footer. |

---

## Read-Only / Implicit Variables

These are set automatically by the .NET runtime or Docker base images and are documented here for completeness.

| Variable | Set By | Description |
|---|---|---|
| `DOTNET_RUNNING_IN_CONTAINER` | .NET base image | Set to `true` by all official Microsoft .NET Docker images. MCP Explorer uses this to resolve `localhost` MCP server URLs to `host.docker.internal` automatically, so connections to your host machine work without any manual configuration. |

---

## Quick Reference

```bash
# Minimal docker run (single container)
docker run -p 8090:8080 \
  -v mcp-data:/data \
  ghcr.io/your-org/mcp-explorer:latest

# With custom client name and existing settings
docker run -p 8090:8080 \
  -e MCP_CLIENT_NAME=my-team \
  -e MCP_DATA_PATH=/Users/me/Library/Application\ Support/McpExplorer \
  -v "/Users/me/Library/Application Support/McpExplorer":/data \
  ghcr.io/your-org/mcp-explorer:latest

# docker compose — copy .env.example to .env, edit, then:
docker compose up -d
```

---

## Azure Integration Quick Reference

```bash
# .env — enable Azure Key Vault & App Registration browsing in Docker
HOST_AZURE_CONFIG_DIR=~/.azure          # macOS / Linux
# HOST_AZURE_CONFIG_DIR=%USERPROFILE%\.azure  # Windows

# AZURE_CONFIG_DIR is set automatically inside the container
# Do NOT put AZURE_CONFIG_DIR in your .env
```

Then run:

```bash
docker compose up --build
```

MCP Explorer will pick up your `az login` session and show "Connected" in the Azure Context Banner on the Connections page.

---

## Dev Tunnels Quick Reference

```bash
# .env — reuse your host devtunnel login inside Docker
# macOS:
HOST_DEVTUNNELS_DIR=/Users/<you>/Library/Application\ Support/DevTunnels
# Linux:
# HOST_DEVTUNNELS_DIR=/home/<you>/.devtunnels
# Windows:
# HOST_DEVTUNNELS_DIR=C:\Users\<you>\AppData\Local\DevTunnels
```

Then run:

```bash
docker compose up -d
```

MCP Explorer will inherit your host `devtunnel user login` session and start tunnels without prompting for device-code authentication.
