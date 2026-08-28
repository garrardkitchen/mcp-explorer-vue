---
title: "Configuring Models"
description: "Add LLM providers and models for use in Chat."
weight: 1
---

## Overview

The Models page is where you configure the LLMs that power the Chat view. MCP Explorer supports any OpenAI-compatible API endpoint.

<img src="/images/screenshots/03-models-list.png" alt="AI Models page showing GPT-5 and GPT-4o configured with provider badges, endpoints, and masked API keys" style="max-width:700px;border-radius:8px;border:1px solid #e2e8f0;" />

*The AI Models page lists all configured LLMs. Each entry shows its friendly name, provider badge (e.g. AzureAIFoundry), model ID, endpoint URL, and masked API key. The default model is highlighted — it is pre-selected in the Send to LLM picker.*

---

## Supported Providers

| Provider | Transport | Notes |
|----------|-----------|-------|
| **OpenAI** | HTTPS | GPT-4o, GPT-4, GPT-3.5 |
| **Azure OpenAI** | HTTPS | Requires deployment name + API version |
| **Azure AI Foundry Model** | HTTPS | Direct access to a model deployment |
| **Azure AI Foundry Project Agent** | HTTPS | Invokes a versioned project agent or hosted agent endpoint through the Responses API |
| **Ollama** | HTTP (local) | Any locally running model |
| **LM Studio** | HTTP (local) | OpenAI-compatible endpoint |
| **Any OpenAI-compatible API** | HTTPS/HTTP | Custom base URL |

---

## Adding a Model

1. Click **Add Model**
2. Select the **Provider** from the dropdown
3. Fill in:
   - **Name** — a friendly display name (editable after saving)
   - **Base URL** — the API endpoint
   - **API Key** — click the 👁 eye icon to reveal / hide
   - **Model ID** — the model identifier (e.g. `gpt-4o`, `llama3.2`)
   - **Deployment name** (Azure only)
   - **API Version** (Azure only)
4. Click **Save**

Use **Test** on a saved model card to send a small live probe with the configured credentials. The probe can incur a small model charge.

---

## Editing a Model

Click the **pencil icon** on any model row. All fields are editable including the name. The provider dropdown updates the form to show the relevant fields.

---

## Show / Hide API Keys

API key fields have an **eye icon** (👁) toggle. Click it to reveal the key temporarily — useful when verifying you've entered the correct value.

---

## Deleting a Model

Click the **trash icon** and confirm. Any Chat sessions using this model will lose their model selection.

---

## Selecting a Model in Chat

Once configured, models appear in the **Model** dropdown in the Chat view. Select any model to use it for the current conversation.

---

## Azure AI Foundry project agents

Select **Azure AI Foundry Project Agent** to configure:

- **Project Endpoint** — the project-level endpoint, such as `https://resource.services.ai.azure.com/api/projects/project-name`
- **Agent Invocation**:
  - **Versioned agent** — invokes a named immutable agent version through the project Responses API. This is the backward-compatible default.
  - **Hosted agent endpoint** — invokes `.../agents/{agentName}/endpoint/protocols/openai/responses`, as required for hosted agents.
- **Agent Name** — required for both invocation modes
- **Agent Version** — required only for **Versioned agent**. A hosted endpoint uses its Foundry endpoint configuration to select the deployed version and route traffic.
- **Authentication** — **Default Azure Credential** (default) or **API Key**

Enter the project-level URL in **Project Endpoint** for both modes; do not paste the complete `/agents/.../responses` URL. Default Azure Credential uses the application's managed identity when hosted and can use your Azure CLI login for local development. API keys are encrypted in the local preferences store.

After entering the project endpoint and credentials, select **Refresh** beside **Agent Name**. MCP Explorer reads the project's available agents and versions. In **Versioned agent** mode, selecting an agent chooses its newest version; selecting another version also reloads that immutable version's instructions into the read-only **System Prompt** field. In **Hosted agent endpoint** mode, no version is pinned because the endpoint configuration owns version selection.

Agent discovery is read-only and does not create, update, deploy, or configure a Foundry agent endpoint. Before testing hosted mode, ensure the hosted agent is active and has a Responses protocol endpoint configured in Foundry. The configured identity needs project data-plane read and invocation access.

Agent instructions and tool declarations remain controlled by Foundry. Versioned declarative agents cannot receive request-level `tools` when an agent is specified. To use an MCP tool with a versioned agent, add a function tool with the same name and compatible input schema to that agent version. Hosted agents control tool behavior in their implementation. When either mode returns a matching function call, MCP Explorer invokes the tool through a selected MCP connection and submits the result before continuing the answer.

Selected connections must be active, and tool names must be unique across them. A Foundry function call is rejected unless a selected connection provides the matching tool. MCP credentials remain in MCP Explorer; they are not sent to Foundry. Function arguments and results do cross the Foundry trust boundary.

Foundry-managed MCP tools can require user approval according to the agent configuration. Chat displays the server label, tool name, and hidden-by-default arguments with **Approve** and **Deny** actions. The response remains in progress until a decision is made; MCP Explorer never approves these calls automatically.

> **info:** If Azure returns “Hosted agents can only be called through the agent endpoint,” edit the model and select **Hosted agent endpoint** under **Agent Invocation** before testing again.
