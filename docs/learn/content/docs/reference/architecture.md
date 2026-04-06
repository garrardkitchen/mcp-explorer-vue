---
title: "Architecture"
description: "MCP Explorer X architecture and deployment modes."
weight: 2
---

## Architecture Overview

MCP Explorer X follows a clean architecture pattern with a Vue 3 SPA frontend and an ASP.NET Core backend.

```mermaid
flowchart TB
    subgraph BROWSER["🌐 Browser"]
        SPA["⚡ Vue 3 · Vite · PrimeVue 4\nSingle-Page Application"]
    end

    subgraph SINGLE["🐳 Single Container (docker run)"]
        GW["🔀 YARP Gateway :8080\nStatic SPA host + /api/** proxy"]
        API["⚙️ ASP.NET Core API :5000"]
        GW -->|reverse proxy| API
    end

    subgraph DATA["💾 Persistent Data"]
        FILE["/app/data/data.json\nConnections · Models · Workflows\nSettings · Chat History"]
    end

    subgraph MCP["🔌 MCP Servers"]
        S1["Server A (stdio)"]
        S2["Server B (SSE)"]
        S3["Server C (HTTP)"]
    end

    BROWSER -->|HTTP/SSE| GW
    API -->|read/write| FILE
    API -->|MCP protocol| S1
    API -->|MCP protocol| S2
    API -->|MCP protocol| S3
```

---

## Deployment Modes

### Single Container

The default deployment — everything in one image.

```bash
docker run -d \
  -p 8090:8080 \
  -v mcp-explorer-data:/app/data \
  ghcr.io/your-username/mcp-explorer-x:latest
```

```mermaid
flowchart LR
    Browser -->|":8090"| Container["Single Container\nYARP + API + SPA"]
    Container -->|"/app/data"| Volume["Named Volume"]
    Container -->|"MCP protocol"| MCP["MCP Servers"]
```

---

### Docker Compose (Separate Services)

For teams or when you want independent scaling of frontend and backend.

```bash
docker compose up -d
```

```mermaid
flowchart LR
    Browser -->|":8090"| FE["Frontend Container\nNginx + Vite SPA"]
    FE -->|"/api/**"| BE["Backend Container\nASP.NET Core API :5000"]
    BE -->|"/app/data"| Volume["Named Volume"]
    BE -->|"MCP protocol"| MCP["MCP Servers"]
```

---

## Data Flow

```mermaid
sequenceDiagram
    participant U as User
    participant SPA as Vue SPA
    participant API as ASP.NET Core API
    participant MCP as MCP Server
    participant DB as data.json

    U->>SPA: Open app
    SPA->>API: GET /api/connections
    API->>DB: Read connections
    DB-->>API: Connection list
    API-->>SPA: JSON response
    SPA-->>U: Render connections

    U->>SPA: Invoke tool
    SPA->>API: POST /api/tools/invoke
    API->>MCP: Tool call (MCP protocol)
    MCP-->>API: Tool result
    API-->>SPA: SSE stream
    SPA-->>U: Render response
```

---

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Frontend | Vue 3, Vite, PrimeVue 4, TypeScript |
| Backend | ASP.NET Core 9, C# |
| MCP SDK | ModelContextProtocol 1.2.0 |
| Gateway | YARP reverse proxy |
| Persistence | JSON file (single file, no database) |
| Container | Docker, multi-stage build |
