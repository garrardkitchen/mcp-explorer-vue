---
title: "Quick Start"
description: "Run MCP Explorer X with Docker in under 2 minutes."
weight: 1
---

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) 20.10+ installed and running
- A terminal / command prompt

---

## Option 1 — Single Container (`docker run`)

The fastest way to get started. One command, one container.

```bash
docker run -d \
  --name mcp-explorer \
  -p 8090:8080 \
  -v mcp-explorer-data:/app/data \
  ghcr.io/your-username/mcp-explorer-x:latest
```

Then open **http://localhost:8090** in your browser.

| Flag | Purpose |
|------|---------|
| `-p 8090:8080` | Map container port 8080 → host port 8090 |
| `-v mcp-explorer-data:/app/data` | Persist connections and settings across restarts |

---

## Option 2 — Docker Compose

Better for long-running or team environments. Supports separate frontend and backend services.

**1. Download the compose file:**

```bash
curl -o docker-compose.yml \
  https://raw.githubusercontent.com/your-username/mcp-explorer-x/main/docker-compose.yml
```

**2. Create a `.env` file (optional but recommended):**

```bash
# .env
MCP_CLIENT_NAME=my-team-explorer
```

**3. Start the services:**

```bash
docker compose up -d
```

Open **http://localhost:8090** in your browser.

---

## Verify It's Running

You should see the Connections page — MCP Explorer X's home screen.

![Connections page — the landing screen after startup](/images/screenshots/home-connections.png)
*The Connections page is shown on startup. It lists all your saved MCP server connections.*

---

## Next Steps

- [Add your first connection](../../connections/managing-connections/) — connect to an MCP server
- [Explore Tools](../../tools/browsing-tools/) — browse and invoke MCP tools
- [Start chatting](../../chat/chat-overview/) — use an LLM with MCP tool calling
