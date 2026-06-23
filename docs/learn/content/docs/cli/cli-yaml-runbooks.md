---
title: "CLI — YAML MCP Runbooks (NEW)"
description: "Declare MCP connections, auth, scheduling, and chained tool calls in YAML for mcp-http mcp runbook."
weight: 3
---

## NEW: `mcp runbook`

Use a YAML file to declare MCP server connection details, authentication, parameters, scheduling, and multi-step chaining.

```bash
mcp-http mcp runbook --file ./runbook.yaml
```

Validate only (no calls):

```bash
mcp-http mcp runbook --file ./runbook.yaml --validate-only
```

---

## YAML capabilities (NEW)

- Declarative MCP connection blocks
- Auth schemes: **bearer**, **apikey**, **basic**, **customHeaders**, **azureClientCredentials**
- Azure Key Vault secret references for tokens/keys/passwords/client IDs/client secrets
- Scheduled execution:
  - `repeat` N times
  - or run at `everySeconds` frequency for `forSeconds`
- Step output chaining via templates like `{{ steps.stepId.property }}`
- Runtime iteration token: `{{ iteration.index }}`
- Environment token: `{{ env.MY_VAR }}`

---

## Example runbook

```yaml
version: "1"
defaultConnection: weather
connections:
  - name: weather
    endpoint: https://example.com/mcp
    auth:
      type: bearer
      tokenFromKeyVault:
        vaultName: my-kv
        secretName: weather-token

steps:
  - id: query
    tool: search_weather
    params:
      city: London

  - id: summarize
    tool: summarize_weather
    params:
      input: "{{ steps.query.result }}"
      runNumber: "{{ iteration.index }}"

schedule:
  repeat: 3
  everySeconds: 10
  forSeconds: 30
```

---

## Security notes (.NET + OWASP aligned)

- Prefer Key Vault references over inline secrets.
- Keep runbook files out of source control when they include sensitive values.
- Use `--validate-only` in CI before execution.
- Use least-privilege app registrations and vault access policies.
- Avoid logging raw secret values; redact sensitive output in pipelines.
