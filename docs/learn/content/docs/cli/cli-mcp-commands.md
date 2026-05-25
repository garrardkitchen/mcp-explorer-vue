---
title: "CLI — MCP Commands"
description: "Reference for mcp-http mcp commands: list connections offline, and connect to MCP servers to list tools, resources, prompts, templates, or invoke tools."
weight: 2
---

## Overview

The `mcp` branch of the CLI operates directly on the MCP connections stored in your `settings.json`. Most commands open a live connection to the MCP server — no browser or running API instance is required.

```bash
mcp-http mcp --help
```

<img src="/images/screenshots/cli-02-mcp-connections.png" alt="Terminal showing mcp-http mcp connections output — a rounded table of saved MCP connections with Name, Endpoint, Auth, Group, and Created columns" style="max-width:700px;border-radius:8px;border:1px solid #e2e8f0;" />

---

## Name Matching

All `mcp` commands that accept `--name` use a two-step lookup:

1. **Exact match** — if a connection name matches `--name` exactly, it is used.
2. **Partial match** — if no exact match is found, a case-insensitive substring search is performed. If exactly one connection matches, it is used. If multiple connections match, the CLI prints the candidates and exits without connecting.

```bash
# Exact match
mcp-http mcp tools --name "Weather API"

# Partial match (matches any connection whose name contains "weather")
mcp-http mcp tools --name weather
```

---

## OAuth Warning

Connections that use **OAuth** authentication require a running API instance to handle the OAuth callback. When such a connection is resolved, the CLI prints a warning and still attempts to connect.

---

## `mcp connections`

List all saved MCP connections **offline** — no network connection required.

```bash
mcp-http mcp connections

# From a custom data directory
mcp-http --data-path "/path/to/data" mcp connections
```

**Output columns:** Name · Endpoint · Auth mode · Group · Created date.

---

## `mcp tools`

Connect to an MCP server and list all tools it exposes.

```bash
mcp-http mcp tools --name "My Server"
```

**Options:**

| Option | Description |
|--------|-------------|
| `--name <text>` | Connection name (exact or partial match) |

**Output:** A table of tool names and descriptions. Connects, fetches the tool list, then disconnects.

---

## `mcp resources`

Connect to an MCP server and list all resources it exposes.

```bash
mcp-http mcp resources --name "My Server"
```

**Options:**

| Option | Description |
|--------|-------------|
| `--name <text>` | Connection name (exact or partial match) |

**Output:** A table of resource URIs, names, and MIME types.

---

## `mcp prompts`

Connect to an MCP server and list all prompts it exposes.

```bash
mcp-http mcp prompts --name "My Server"
```

**Options:**

| Option | Description |
|--------|-------------|
| `--name <text>` | Connection name (exact or partial match) |

**Output:** A table of prompt names, descriptions, and argument names. Required arguments are shown in **bold**.

---

## `mcp templates`

Connect to an MCP server and list all resource templates it exposes.

```bash
mcp-http mcp templates --name "My Server"
```

**Options:**

| Option | Description |
|--------|-------------|
| `--name <text>` | Connection name (exact or partial match) |

**Output:** A table of template URI patterns and descriptions.

---

## `mcp invoke`

Connect to an MCP server and invoke a tool, printing the result.

```bash
# Single parameter as key=value
mcp-http mcp invoke --name "My Server" --tool echo --param message=hello

# Parameters as a JSON object
mcp-http mcp invoke --name "My Server" --tool search --params '{"query":"dotnet","limit":10}'

# Mix: --params sets defaults, --param overrides individual keys
mcp-http mcp invoke --name "My Server" --tool search \
  --params '{"query":"dotnet"}' \
  --param limit=5

# From a custom data directory
mcp-http --data-path "/path/to/data" mcp invoke --name "My Server" --tool echo --param message=hi
```

**Options:**

| Option | Description |
|--------|-------------|
| `--name <text>` | Connection name (exact or partial match) |
| `--tool <text>` | Tool name to invoke |
| `--param key=value` | Single parameter (repeatable). Values are auto-parsed as JSON literals (numbers, booleans, arrays, objects) with fallback to string |
| `--params <json>` | All parameters as a JSON object string |

### Parameter Parsing

`--param` values are automatically coerced to native types:

| Value | Parsed as |
|-------|-----------|
| `42` | `number` |
| `true` / `false` | `boolean` |
| `[1,2,3]` | JSON array |
| `{"a":1}` | JSON object |
| `hello` | `string` |

When both `--params` and `--param` are provided, the per-key `--param` values win on conflict.

**Output:** A rounded panel containing the tool's raw result string.

### Example — combining `--params` and `--param`

```bash
mcp-http mcp invoke \
  --name "Data Server" \
  --tool query \
  --params '{"database":"prod","timeout":30}' \
  --param limit=100 \
  --param verbose=true
```

Effective parameters sent to the tool: `{ database: "prod", timeout: 30, limit: 100, verbose: true }`.
