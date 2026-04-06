---
title: "Chat Overview"
description: "Streaming AI chat with MCP tool calling."
weight: 1
---

## Overview

The Chat view gives you a streaming conversation interface backed by any LLM you've configured. What makes it powerful is **automatic MCP tool calling** — the LLM can invoke any tool from your active connections mid-conversation, and MCP Explorer handles the round-trip transparently.

---

## Starting a Chat

1. Click **New Session** in the left sidebar to create a fresh conversation
2. Select which **Connections** to make available for tool calling from the multiselect dropdown
3. Choose your **Model** (defaults to GPT-4o)
4. Type your message and press **Ctrl+Enter** or click the **Send** button

<img src="/images/screenshots/29-chat-connection-selected.png" alt="Chat session with mcp server connection selected" style="max-width:100%;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.15);cursor:zoom-in">

*A new chat session with the `mcp server` connection selected. The connection badge appears next to the model selector, confirming the LLM has access to all MCP tools from that server.*

---

## Sending a Message

Type your message in the input box at the bottom of the chat area. Responses stream in real time — you see tokens appearing as the model generates them. Token usage (input ↑ / output ↓) is shown under each assistant response.

<img src="/images/screenshots/30-chat-message-typed.png" alt="who is garrard? typed in chat input" style="max-width:100%;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.15);cursor:zoom-in">

*"who is garrard?" ready to send. Press **Ctrl+Enter** to submit.*

<img src="/images/screenshots/31-chat-response.png" alt="LLM response streamed into chat with MCP tool call" style="max-width:100%;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.15);cursor:zoom-in">

*GPT-4o's streamed response to "who is garrard?". Notice the 🔧 **Calling tool: who_is** badge — the model automatically invoked an MCP tool mid-conversation to answer the question. Thinking time (🤔 2.5s) and token cost (💰 1158↑ 14↓) are shown beneath the message.*

---

## Slash Commands

Type `/` in the message input to open the command palette. All available commands appear with descriptions — navigate with **↑↓** arrow keys, press **Enter** to select, or **Esc** to dismiss.

<img src="/images/screenshots/33-chat-slash-menu.png" alt="Slash command menu showing all available commands" style="max-width:100%;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.15);cursor:zoom-in">

*Typing `/` reveals the full command palette with categories for MCP Prompts and Chat Controls.*

<img src="/images/screenshots/32-chat-slash-prompt.png" alt="Prompt Picker dialog showing MCP prompts after /prompt command" style="max-width:100%;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.15);cursor:zoom-in">

*After pressing Enter on `/prompt`, the **Prompt Picker** dialog opens. Select any MCP prompt from the connected server, fill in its arguments, then click **Run in Chat** to inject it directly into the conversation.*

| Command | Description |
|---------|-------------|
| `/prompt` | Browse & inject an MCP prompt template from connected servers |
| `/stats` | Show token usage: input / output / total for this session |
| `/report` | Copy entire conversation as formatted Markdown to clipboard |
| `/system` | Open the system prompt editor for the active model |
| `/model` | Quick-switch the active AI model |
| `/clear` | Clear all messages in the current session |

---

## Tool Calling

When the LLM decides to use a tool, MCP Explorer:
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

MCP Explorer detects and masks sensitive data (API keys, passwords, secrets) in:
- Your messages before they're sent
- Tool response content before display

Masked values are shown as `●●●●●●●●` with a reveal toggle.
