---
title: "Using Resource Templates"
description: "Expand MCP URI templates with runtime parameters to fetch dynamic resources."
weight: 1
---

## What are Resource Templates?

Resource Templates are URI patterns that MCP servers expose with placeholders — for example `file:///{path}`, `db:///{table}/{id}`, or `api:///{endpoint}?query={q}`. By filling in the placeholders, you resolve the template to a concrete URI and fetch the corresponding resource.

They're distinct from static Resources in that they're dynamic — the same template can produce unlimited concrete resources.

---

## Finding Templates

The Resource Templates tab is accessible from the **Resources** page (tab switcher at the top). Templates are listed with:
- The URI pattern (e.g. `file:///{path}`)
- A description from the MCP server
- The server it belongs to

Use the search bar to filter templates by name or URI pattern.

---

## Expanding a Template

1. Click a template card to expand it
2. Fill in the placeholder fields — each `{placeholder}` in the URI becomes a form field
3. Click **Expand** to resolve the URI
4. The resolved resource content is fetched and displayed inline

### Example

Template: `db:///{table}/{id}`

| Field | Value |
|-------|-------|
| `table` | `users` |
| `id` | `42` |

Resolves to → `db:///users/42` → fetches user record #42

---

## Favourites

Star templates you use frequently. Toggle **Show favourites first** to pin starred templates to the top of the list. This preference persists across sessions.

---

## Template vs Resource

| | Resource | Resource Template |
|-|----------|------------------|
| URI | Fixed | Parameterised pattern |
| Fetch | Direct | Expand → fetch |
| Use case | Static files, config | Dynamic records, queries |
| Count | Finite list | Unlimited combinations |
