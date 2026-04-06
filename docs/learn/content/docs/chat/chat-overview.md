---
title: "Chat Overview"
description: "Streaming AI chat with MCP tool calling."
weight: 1
---

## Overview

The Chat view gives you a streaming conversation interface backed by any LLM you've configured. What makes it powerful is **automatic MCP tool calling** — the LLM can invoke any tool from your active connections mid-conversation, and MCP Explorer X handles the round-trip transparently.

![Chat interface with message stream, model selector, and active tool badges](/images/screenshots/chat-view.png)
*The Chat view shows a streaming conversation. Active tool invocations appear as badges in the message stream, and token usage is displayed per response.*

---

## Starting a Chat

1. Select a **Model** from the dropdown (top-right)
2. Select which **Connections** to make available for tool calling
3. Type your message and press **Enter** or click **Send**

Responses stream in real time via SSE. Tool calls appear inline as the LLM invokes them.

---

## Slash Commands

Type `/` in the message input to access slash commands:

| Command | Description |
|---------|-------------|
| `/clear` | Clear the current conversation |
| `/prompt <name>` | Insert a saved MCP prompt into the chat |
| `/help` | Show all available slash commands |

---

## Tool Calling

When the LLM decides to use a tool, MCP Explorer X:
1. Shows an **active tool badge** indicating which tool is running
2. Sends the tool call to the appropriate MCP server
3. Returns the result to the LLM to continue its response

Tool call details (inputs and outputs) are shown inline in the message stream — fully expandable.

---

## Token Usage

Each LLM response shows a token usage summary:
- **Prompt tokens** — input sent to the model
- **Completion tokens** — tokens in the response
- **Total** — combined

---

## Conversation Management

- **New Chat** — start a fresh conversation (retains model and connection selection)
- **Clear** — clear messages without changing settings
- Chat history is persisted per session

---

## Sensitive Data in Chat

MCP Explorer X detects and masks sensitive data (API keys, passwords, secrets) in:
- Your messages before they're sent
- Tool response content before display

Masked values are shown as `●●●●●●●●` with a reveal toggle.
