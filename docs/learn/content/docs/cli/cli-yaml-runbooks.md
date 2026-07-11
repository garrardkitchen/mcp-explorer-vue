---
title: "CLI — YAML Runbooks (MCP + HTTP) (NEW)"
description: "Declare MCP and HTTP runbooks in YAML with validation, assertions, scheduling, and chained outputs for mcp-http."
weight: 3
---

## NEW: `mcp runbook` and `http runbook`

Use YAML files to declare either MCP tool workflows or HTTP endpoint workflows with scheduling, assertions, and multi-step chaining.

```bash
mcp-http mcp runbook --file ./runbook.yaml
mcp-http http runbook --file ./http-runbook.yaml
```

Validate syntax and structure only (no calls):

```bash
mcp-http mcp runbook --file ./runbook.yaml --validate-only
mcp-http http runbook --file ./http-runbook.yaml --validate-only
```

---

## YAML capabilities (NEW)

- Declarative MCP connection blocks
- Auth schemes: **bearer**, **apikey**, **basic**, **customHeaders**, **azureClientCredentials**
- Azure Key Vault secret references for tokens/keys/passwords/client IDs/client secrets
- Step output chaining via templates like `{{ steps.stepId.property }}`
- Assertion checks per step (`assert`) to mark pass/fail from result data
- Assertion failure behavior control with `continueOnAssertFailure`
- Scheduled execution:
  - `repeat` N times
  - or run at `everySeconds` frequency for `forSeconds`
- Runtime iteration token: `{{ iteration.index }}`
- Environment token: `{{ env.MY_VAR }}`

---

## Example MCP runbook with assertions

```yaml
version: "1"
defaultConnection: weather
continueOnAssertFailure: false
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
    assert:
      path: result.status
      operator: equals
      value: ok

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

## Example HTTP runbook with assertions

```yaml
version: "1"
defaultConnection: weather-http
continueOnAssertFailure: true
connections:
  - name: weather-http
    endpoint: https://example.com/weather?city={city}
    auth:
      type: custom
      headers: []
steps:
  - id: first
    inputs:
      city: London
    assert:
      path: isSuccess
      operator: equals
      value: true

  - id: second
    inputs:
      previousCity: "{{ steps.first.body.city }}"
    assert:
      path: statusCode
      operator: equals
      value: 200

schedule:
  repeat: 2
```

---

## Assertion operators

| Operator | Meaning |
|----------|---------|
| `equals` | Value at `path` equals `value` |
| `notEquals` | Value at `path` does not equal `value` |
| `contains` | String value at `path` contains `value` |
| `exists` | `path` is present in result |

Use `path: "$"` (or omit path) to assert the full step result.

---

## Validation feedback and hints

When parsing or validation fails, the CLI now prints:

- exact syntax/structure errors
- helpful hints for common YAML issues
- hints for fixing step ids, connections, schedule fields, endpoint selectors, and assertion config

---

## Security notes (.NET + OWASP aligned)

- Prefer Key Vault references over inline secrets.
- Keep runbook files out of source control when they include sensitive values.
- Use `--validate-only` in CI before execution.
- Use least-privilege app registrations and vault access policies.
- Avoid logging raw secret values; redact sensitive output in pipelines.
