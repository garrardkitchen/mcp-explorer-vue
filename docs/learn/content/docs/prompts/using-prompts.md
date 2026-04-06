---
title: "Using Prompts"
description: "Execute MCP prompts and pipe results to an LLM."
weight: 1
---

## Overview

MCP Prompts are parameterised message templates that MCP servers expose. They let you capture common workflows — code review patterns, data analysis templates, document generators — and invoke them with a few inputs.

<img src="/images/screenshots/09-prompts-connection-selected.png" alt="Prompts view with mcp server selected listing 3 prompts" style="max-width:700px;border-radius:8px;border:1px solid #e2e8f0;" />

*Select a connection to load its prompts. Each prompt shows its name, description, and argument count.*

---

## Executing a Prompt

1. Click a prompt to open its detail panel
2. Fill in any required arguments

<img src="/images/screenshots/11-prompts-argument-filled.png" alt="one_sentence_summary prompt with text argument filled in as ms aspire" style="max-width:700px;border-radius:8px;border:1px solid #e2e8f0;" />

*Each prompt argument has a label, description, and validation indicator. Fill in the values and click Execute.*

3. Click **Execute** — the rendered MCP messages appear as JSON in the result panel

<img src="/images/screenshots/25-prompts-execute-result.png" alt="Prompt result showing rendered output with Send to LLM and Copy buttons" style="max-width:700px;border-radius:8px;border:1px solid #e2e8f0;" />

*After execution, the result is displayed inline. The **Send to LLM** button appears in the result toolbar — click it to pipe the prompt messages directly to your configured LLM.*

4. Click **Send to LLM** — a model picker popover appears

<img src="/images/screenshots/26-prompts-send-to-llm-popover.png" alt="Model picker popover with GPT-4o selected and Cancel/Send buttons" style="max-width:500px;border-radius:8px;border:1px solid #e2e8f0;" />

*Choose a model from the dropdown (your configured LLM providers are listed here — GPT-4o is the default) and click **Send** to stream the response.*

5. The **LLM Response** tab activates with the streamed result

<img src="/images/screenshots/27-prompts-llm-response-tab.png" alt="LLM Response tab showing the GPT-4o generated answer to the prompt" style="max-width:700px;border-radius:8px;border:1px solid #e2e8f0;" />

*The LLM Response tab shows the streamed reply from GPT-4o. Results are rendered as Markdown with syntax highlighting.*

---

## Prompt Arguments

Arguments are generated dynamically from the prompt's schema. Each field shows:
- A label and description (from the server schema)
- Validation (required vs optional)
- Inline error messages

## Document Generation

The `summary_benefits_and_references` prompt demonstrates the document generation capability — it produces a full Markdown document with a summary, key benefits, and references for any topic.

<img src="/images/screenshots/19-prompts-document-selected.png" alt="summary_benefits_and_references prompt selected with topic argument field" style="max-width:700px;border-radius:8px;border:1px solid #e2e8f0;" />

<img src="/images/screenshots/20-prompts-document-result.png" alt="Document output showing formatted Markdown with summary, bullets, and references" style="max-width:700px;border-radius:8px;border:1px solid #e2e8f0;" />

*The document prompt generates structured Markdown with a summary paragraph, benefit bullets, and reference list — ready to copy or pipe to an LLM.*

---

## Favourites

Star any prompt to mark it as a favourite. Toggle **Show favourites first** in the toolbar to pin starred prompts to the top of the list. This preference is persisted across sessions.

---

## Markdown Rendering

Prompt output is rendered as Markdown, including:
- Code blocks with syntax highlighting
- Tables
- Inline formatting

---

## Send to Chat

After executing a prompt, the **Send to Chat** button opens the Chat view with the prompt result pre-loaded as the conversation context — letting you continue the interaction with any configured LLM.
