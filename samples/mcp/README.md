# MCP Runbook Samples — Microsoft Learn

These runbooks demonstrate how to use the `mcp-http` CLI to query the
[Microsoft Learn MCP server](https://learn.microsoft.com/api/mcp) — a public,
authentication-free MCP endpoint that exposes documentation search.

---

## Prerequisites

1. **Build or install the CLI**

   ```bash
   # From the repository root
   dotnet build src/Garrard.Mcp.Explorer.Cli/Garrard.Mcp.Explorer.Cli.csproj -c Release
   ```

   Or publish a self-contained binary:

   ```bash
   dotnet publish src/Garrard.Mcp.Explorer.Cli/Garrard.Mcp.Explorer.Cli.csproj \
     -c Release -r linux-x64 --self-contained -o ./dist/cli
   # Then add ./dist/cli to your PATH or invoke it directly
   ```

2. **Network access** — The MS Learn MCP server is hosted at
   `https://learn.microsoft.com/api/mcp`. Ensure your machine can reach it on
   port 443.

3. **(Optional) Discover available tools** — Before running the runbooks, you can
   list the tools exposed by the MS Learn MCP server:

   ```bash
   mcp-http mcp tools --name mslearn \
     --endpoint https://learn.microsoft.com/api/mcp
   ```

   The runbooks in this folder use the **`microsoft_docs_search`** tool, which
   accepts a single `query` parameter.

---

## Samples

### 1. `mslearn-service-limits.yaml` — Azure service limits

Queries for service limits documentation on two Azure services in a single run.

**Run:**

```bash
mcp-http mcp runbook --file samples/mcp/mslearn-service-limits.yaml
```

**Validate only (no network call):**

```bash
mcp-http mcp runbook --file samples/mcp/mslearn-service-limits.yaml --validate-only
```

**Expected output** (representative — actual content varies by server version):

```
╭──────────────── Runbook results ─────────────────╮
│ {                                                 │
│   "keyvault-service-limits": {                    │
│     "content": [                                  │
│       {                                           │
│         "type": "text",                           │
│         "text": "# Azure Key Vault service limits │
│ \n\nKey Vault has the following throttling limits:│
│ ..."                                              │
│       }                                           │
│     ]                                             │
│   },                                              │
│   "servicebus-service-limits": {                  │
│     "content": [                                  │
│       {                                           │
│         "type": "text",                           │
│         "text": "# Azure Service Bus quotas\n\n   │
│ ..."                                              │
│       }                                           │
│     ]                                             │
│   }                                               │
│ }                                                 │
╰───────────────────────────────────────────────────╯
```

---

### 2. `mslearn-assertions.yaml` — Success and failure assertions

Demonstrates both a passing assertion (`exists`) and a deliberately failing
assertion (`equals` with an impossible value).  `continueOnAssertFailure: true`
is set so both steps always execute.

**Run:**

```bash
mcp-http mcp runbook --file samples/mcp/mslearn-assertions.yaml
```

**Expected output:**

```
[search-failure] Assertion failed at '$': expected equals
"this-value-will-never-match" but got { ... }.

╭──────────────── Runbook results ─────────────────╮
│ {                                                 │
│   "search-success": {                             │
│     "content": [{ "type": "text", "text": "..." }]│
│   },                                              │
│   "search-failure": {                             │
│     "content": [{ "type": "text", "text": "..." }]│
│   }                                               │
│ }                                                 │
╰───────────────────────────────────────────────────╯

Assertions failed: 1. Review assertion paths/operators/values.
```

Exit code is **1** when any assertion fails (even with `continueOnAssertFailure`).

---

## Assertion operators reference

| Operator     | What it checks                                  |
|--------------|-------------------------------------------------|
| `exists`     | Path resolves to a non-null value               |
| `equals`     | Deep equality between resolved value and expected |
| `notequals`  | Value differs from expected                     |
| `contains`   | String `actual` contains string `expected`      |

---

## Scheduling (optional)

To repeat a runbook on an interval, add a `schedule` block:

```yaml
schedule:
  repeat: 5          # run 5 times total
  everySeconds: 30   # wait 30 s between runs
```

Or run for a fixed duration:

```yaml
schedule:
  forSeconds: 120    # keep running for 2 minutes
  everySeconds: 15   # polling every 15 s
```

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `Resource temporarily unavailable (learn.microsoft.com:443)` | Check network/firewall access to `learn.microsoft.com:443` |
| `Runbook validation failed: Default connection … is not declared` | Ensure the connection `name` under `connections:` matches `defaultConnection:` |
| `Unsupported auth type 'none'` | Use `type: custom` with an empty `headers: []` for no-auth connections |
| Tool name not found | Run `mcp-http mcp tools` first to confirm the exact tool name exposed by the server |
