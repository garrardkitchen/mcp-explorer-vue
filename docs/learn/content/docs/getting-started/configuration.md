---
title: "Configuration"
description: "Environment variables and persistent data configuration."
weight: 2
---

## Environment Variables

MCP Explorer is configured via environment variables. When using Docker Compose, place these in a `.env` file alongside `docker-compose.yml`.

| Variable | Default | Description |
|----------|---------|-------------|
| `MCP_CLIENT_NAME` | `mcp-explorer` | Name sent in the `User-Agent` header and MCP `ClientInfo` when connecting to MCP servers |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Set to `Development` for verbose logging |

### Example `.env`

```bash
# .env
MCP_CLIENT_NAME=acme-mcp-explorer
```

---

## Persistent Data

MCP Explorer stores all state — connections, models, workflows, chat history, and settings — in a single JSON file at `/app/data/data.json` inside the container.

**Always mount a volume** to avoid losing data on container restart:

```bash
# docker run
-v mcp-explorer-data:/app/data

# docker-compose.yml
volumes:
  - mcp-explorer-data:/app/data
```

---

## User-Agent Header

When MCP Explorer connects to an MCP server over HTTP/SSE, it sends:

```
User-Agent: <MCP_CLIENT_NAME>/<version> (<hostname>)
```

For example:
```
User-Agent: mcp-explorer/1.0.0 (my-laptop.local)
```

This helps MCP server operators identify requests from MCP Explorer in their logs.

---

## Port Mapping

The default port inside the container is **8080**. Map it to any host port you prefer:

```bash
# Use port 3000 on the host
docker run -p 3000:8080 ghcr.io/your-username/mcp-explorer-x:latest
```
