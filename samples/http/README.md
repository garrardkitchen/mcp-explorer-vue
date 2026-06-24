# HTTP Runbook Samples — httpbin.org

These runbooks demonstrate how to use the `mcp-http` CLI to execute HTTP API
calls from a YAML file, with assertions, scheduling, and step chaining.

They use [httpbin.org](https://httpbin.org), a public HTTP testing service that
requires no authentication.

---

## Prerequisites

### 1. Build or install the CLI

```bash
# From the repository root
dotnet build src/Garrard.Mcp.Explorer.Cli/Garrard.Mcp.Explorer.Cli.csproj -c Release
```

Or publish a self-contained binary:

```bash
dotnet publish src/Garrard.Mcp.Explorer.Cli/Garrard.Mcp.Explorer.Cli.csproj \
  -c Release -r linux-x64 --self-contained -o ./dist/cli
```

### 2. Create the required API definitions

HTTP runbooks reference **saved API definitions** by name.  You must add these
definitions to MCP Explorer before running the samples.

#### Option A — MCP Explorer UI

1. Open MCP Explorer and navigate to **HTTP APIs → New**.
2. Create the two definitions below (one at a time).

| Field         | HttpBin Get              | HttpBin Status                   |
|---------------|--------------------------|----------------------------------|
| **Name**      | `HttpBin Get`            | `HttpBin Status`                 |
| **Base URL**  | `https://httpbin.org`    | `https://httpbin.org`            |
| **Path**      | `/get`                   | `/status/{code}`                 |
| **Method**    | `GET`                    | `GET`                            |
| **Auth**      | None                     | None                             |

#### Option B — CLI import

Export the definitions from another MCP Explorer instance and import them:

```bash
mcp-http http api import --file httpbin-definitions.json --password <password>
```

> **Only `httpbin-assertions.yaml` requires `HttpBin Status`.**  
> **`httpbin-basic.yaml` requires both `HttpBin Get` and `HttpBin Status`.**

---

## Samples

### 1. `httpbin-basic.yaml` — Basic HTTP GET calls

Executes two GET requests against httpbin in a single pass.

**Run:**

```bash
mcp-http http runbook --file samples/http/httpbin-basic.yaml
```

**Validate only (no network call):**

```bash
mcp-http http runbook --file samples/http/httpbin-basic.yaml --validate-only
```

**Expected output:**

```
╭──────────────── Runbook results ─────────────────╮
│ {                                                 │
│   "get-headers": {                                │
│     "statusCode": 200,                            │
│     "latencyMs": 312,                             │
│     "isSuccess": true,                            │
│     "errorMessage": null,                         │
│     "body": {                                     │
│       "args": {},                                 │
│       "headers": {                                │
│         "Accept": "*/*",                          │
│         "Host": "httpbin.org"                     │
│       },                                          │
│       "origin": "203.0.113.42",                   │
│       "url": "https://httpbin.org/get"            │
│     }                                             │
│   },                                              │
│   "get-200-status": {                             │
│     "statusCode": 200,                            │
│     "latencyMs": 198,                             │
│     "isSuccess": true,                            │
│     "errorMessage": null,                         │
│     "body": null                                  │
│   }                                               │
│ }                                                 │
╰───────────────────────────────────────────────────╯
```

---

### 2. `httpbin-assertions.yaml` — Success and failure assertions

Demonstrates both a passing assertion (`statusCode equals 200`) and a
deliberately failing assertion (`statusCode equals 999`).
`continueOnAssertFailure: true` is set so both steps always execute.

**Run:**

```bash
mcp-http http runbook --file samples/http/httpbin-assertions.yaml
```

**Expected output:**

```
[assert-failure] Assertion failed at 'statusCode': expected equals 999 but got 200.

╭──────────────── Runbook results ─────────────────╮
│ {                                                 │
│   "assert-success": {                             │
│     "statusCode": 200,                            │
│     "latencyMs": 201,                             │
│     "isSuccess": true,                            │
│     "errorMessage": null,                         │
│     "body": null                                  │
│   },                                              │
│   "assert-failure": {                             │
│     "statusCode": 200,                            │
│     "latencyMs": 188,                             │
│     "isSuccess": true,                            │
│     "errorMessage": null,                         │
│     "body": null                                  │
│   }                                               │
│ }                                                 │
╰───────────────────────────────────────────────────╯

Assertions failed: 1. Review assertion paths/operators/values.
```

Exit code is **1** when any assertion fails (even with `continueOnAssertFailure`).

---

## Step result schema

Every HTTP step result object has these fields, which you can target in
assertions:

| Field          | Type              | Description                              |
|----------------|-------------------|------------------------------------------|
| `statusCode`   | integer           | HTTP response status code                |
| `latencyMs`    | integer           | Round-trip latency in milliseconds       |
| `isSuccess`    | boolean           | `true` when status is 2xx                |
| `errorMessage` | string \| null    | Error message on network/transport failure |
| `body`         | object/string/null| Parsed JSON body, or raw string if not JSON |

**Assertion path examples:**

```yaml
# Assert the whole response exists
assert:
  path: "$"
  operator: exists

# Assert status code
assert:
  path: statusCode
  operator: equals
  value: 200

# Assert nested body field
assert:
  path: body.url
  operator: contains
  value: "httpbin.org"

# Assert success flag
assert:
  path: isSuccess
  operator: equals
  value: true
```

---

## Assertion operators reference

| Operator     | What it checks                                   |
|--------------|--------------------------------------------------|
| `exists`     | Path resolves to a non-null value                |
| `equals`     | Deep equality between resolved value and expected |
| `notequals`  | Value differs from expected                      |
| `contains`   | String `actual` contains string `expected`       |

---

## Scheduling (optional)

Add a `schedule` block to repeat the runbook:

```yaml
schedule:
  repeat: 10         # run 10 times total
  everySeconds: 60   # wait 60 s between runs
```

Or run for a fixed duration:

```yaml
schedule:
  forSeconds: 300    # keep running for 5 minutes
  everySeconds: 30   # poll every 30 s
```

---

## Chaining step results

Use `{{steps.<id>.<field>}}` to pass a value from one step into a later step's
`inputs`:

```yaml
steps:
  - id: get-post
    endpoint: JSONPlaceholder Posts
    inputs:
      id: "1"

  - id: check-user
    endpoint: JSONPlaceholder Users
    inputs:
      # Use the userId returned by the previous step
      id: "{{steps.get-post.body.userId}}"
```

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `Step '…' endpoint '…' was not found` | The API definition name in `endpoint:` must match exactly what is saved in MCP Explorer (case-insensitive) |
| `Step '…' is missing endpoint and endpointId` | Each step must have either `endpoint:` or `endpointId:` |
| `Step '…' endpointId '…' was not found` | Use `mcp-http http api list` to confirm the ID |
| Assertion path error | Use `mcp-http http api invoke --name "HttpBin Get"` to inspect the response shape |
