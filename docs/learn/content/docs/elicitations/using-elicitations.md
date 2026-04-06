---
title: "Using Elicitations"
description: "Respond to MCP tool elicitations with inline input dialogs."
weight: 1
---

## What are Elicitations?

Some MCP tools need additional user input mid-execution — for example, asking which of several options to proceed with, or requesting a confirmation before a destructive operation. This is the MCP *elicitation* feature.

MCP Explorer surfaces these requests as **inline dialogs** that pause the tool execution and wait for your response.

---

## How Elicitations Work

```mermaid
sequenceDiagram
    participant U as User
    participant App as MCP Explorer
    participant T as MCP Tool

    U->>App: Invoke tool
    App->>T: Tool call
    T-->>App: Elicitation request (needs input)
    App-->>U: Show inline input dialog
    U->>App: Provide response
    App->>T: Continue with user input
    T-->>App: Tool result
    App-->>U: Display result
```

---

## Responding to an Elicitation

When a tool makes an elicitation request, an inline prompt appears within the tool card:

1. **Read the prompt** — the tool explains what input it needs

<img src="/images/screenshots/07-elicitation-range-prompt.png" alt="Elicitation dialog showing range selection with Low range and High range radio options, plus title and date fields" style="max-width:700px;border-radius:8px;border:1px solid #e2e8f0;" />

*The `guess_the_number` tool asks for a range, a title, a date, and multi-select options — all in one elicitation form.*

2. **Fill in the form** — the form type depends on what the tool requests:

| Input Type | UI Control | When Used |
|------------|-----------|-----------|
| Short text | Text input | Single-line string |
| Long text | Textarea | Multi-line content |
| Yes/No | Toggle / radio | Boolean confirmation |
| One of few options (≤3) | Radio buttons | Small enum choice |
| One of many options (>3) | Dropdown | Large enum list |
| Multiple selections | Multi-select | Array of choices |

<img src="/images/screenshots/07b-elicitation-filled.png" alt="Elicitation form filled in with Low range selected, title filled, date chosen, options checked" style="max-width:700px;border-radius:8px;border:1px solid #e2e8f0;" />

*Fill in all the form fields — radio buttons, text inputs, date pickers, and multi-select checkboxes are all supported.*

3. **Click Accept** to continue the tool — or **Decline** to cancel the elicitation

<img src="/images/screenshots/07c-elicitation-step2.png" alt="Second elicitation step asking for a numeric guess between 0 and 10" style="max-width:700px;border-radius:8px;border:1px solid #e2e8f0;" />

*After the first response, the tool may make a second elicitation request — here asking for the actual numeric guess.*

<img src="/images/screenshots/07d-elicitation-guess-entered.png" alt="Number input with value 7 entered" style="max-width:700px;border-radius:8px;border:1px solid #e2e8f0;" />

*Enter the value and click Accept to submit.*

4. The tool receives your input and continues execution

<img src="/images/screenshots/07e-elicitation-result.png" alt="Tool result showing the outcome of the guess the number game" style="max-width:700px;border-radius:8px;border:1px solid #e2e8f0;" />

*The final tool result appears after all elicitation steps are complete.*

---

## Declining an Elicitation

Click **Decline** to reject the elicitation. The tool will receive a declined response and may abort or continue with a default value, depending on how it's implemented.

---

## Elicitation History

The Elicitations page shows all past elicitation requests across sessions:
- The tool that requested it
- The prompt text
- Your response (or "Declined")
- Timestamp

This is useful for auditing what data you've provided to tools.

---

## Tips

- Elicitations are tool-server-defined — the prompt text and available options come from the MCP server
- If a tool frequently elicits the same input, consider whether it supports default parameter values that could skip the elicitation
