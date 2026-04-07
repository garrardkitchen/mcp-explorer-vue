---
title: "Managing Connections"
description: "Create, edit, delete, and organise MCP server connections."
weight: 1
---

## Overview

The Connections page is your hub for all MCP server connections. From here you can add new servers, edit existing ones, group them logically, and connect or disconnect at any time.

<img src="/images/screenshots/01-connections-list.png" alt="Connections list showing MCP servers with health indicators, transport type, and connect toggles" style="max-width:700px;border-radius:8px;border:1px solid #e2e8f0;" />

*The Connections page lists all saved MCP servers with live health indicators, transport type, and connection toggles. Use the toolbar to add, import, or export connections.*

---

## Creating a Connection

1. Click **New Connection** in the top-right corner
2. Fill in the connection form:

<img src="/images/screenshots/02-connections-new-form.png" alt="New connection form with fields for name, URL, transport type, and optional headers" style="max-width:700px;border-radius:8px;border:1px solid #e2e8f0;" />

*The connection form uses Streamable HTTP transport, with optional custom headers and OAuth/Azure credentials.*

| Field | Required | Description |
|-------|----------|-------------|
| **Name** | ✅ | A friendly display name |
| **Endpoint URL** | ✅ | The MCP server URL |
| **Transport** | — | Streamable HTTP (the only supported transport) |
| **Group** | ❌ | Assign to a named group for organisation |
| **Description** | ❌ | Free-text notes |
| **Headers** | ❌ | Custom HTTP headers (e.g. `Authorization: Bearer ...`) |

3. Click **Save** — the connection appears in the list immediately.

---

## Connecting & Disconnecting

Click the **toggle** next to any connection to connect or disconnect. The sidebar shows an **active connections badge** with a live count.

---

## Editing a Connection

Click the **pencil icon** on any connection row to edit all fields including the name, URL, transport, and headers.

---

## Deleting a Connection

Click the **trash icon** and confirm the deletion dialog. This is permanent.

---

## Grouping Connections

Assign connections to named groups to keep your list organised. Groups appear as filter tabs above the connections list. Useful when working with many servers across different projects.

Click the **+** icon next to the group tabs to create a new group — give it a name and optionally pick a colour:

<img src="/images/screenshots/48-connections-add-group.png" alt="New Group dialog with name and colour picker" style="max-width:460px;border-radius:8px;box-shadow:0 4px 16px rgba(0,0,0,0.18);" />

*The New Group dialog. Enter a name and pick an accent colour, then click **Create**. You can then assign connections to the group via the **Group** field in the connection form.*

---

## Import & Export

You can export all your connections to an **encrypted JSON file** and import them on another machine or share with a team.

- **Export**: Click **Export Connections** in the top toolbar → enter an encryption passphrase → download the `.json` file
- **Import**: Click **Import Connections** → select the file → enter the passphrase → connections are merged

> **info:** The export file is AES-256 encrypted. Without the passphrase it cannot be read.

---

## Connection Health

Each connection row shows a live health indicator:
- 🟢 **Connected** — server is live and responding
- 🔴 **Disconnected** — not connected
- 🟡 **Error** — connection attempt failed (hover for the error message)

---

## Authentication Modes

MCP Explorer supports three authentication modes when connecting to protected MCP servers.

### Custom Headers

Add any HTTP headers to every request — useful for static bearer tokens, API keys, or proprietary auth schemes.

| Header | Example Value |
|--------|---------------|
| `Authorization` | `Bearer eyJ...` |
| `X-Api-Key` | `sk-...` |

---

### Azure Client Credentials

Authenticate using an Entra ID (Azure AD) app registration. MCP Explorer obtains an OAuth 2.0 client-credentials token and attaches it to every request.

{{< figure src="/images/screenshots/azure-client-credentials-edit.png" alt="Edit Connection dialog showing Azure Client Credentials auth mode with the Azure Context Banner, Tenant ID (auto-detected), Client ID, Key Vault secret reference, and auto-filled Scope fields" class="screenshot" >}}

| Field | Description |
|-------|-------------|
| **Tenant ID** | Your Entra tenant. Auto-populated from `az account show` when Azure is connected. Changes automatically when you switch subscription. |
| **Client ID** | The app registration's Application (client) ID. Use the [Browse App Registrations](#browse-app-registrations) button to pick it from a searchable list. |
| **Client Secret** | The app secret. Type it manually, or use the [Key Vault picker](#key-vault-secret-picker) to reference a secret stored in Azure Key Vault. |
| **Scope** | The OAuth scope — e.g. `api://<resource-id>/.default`. Auto-filled when you pick an App Registration. |
| **Authority Host** | Optional. Defaults to `https://login.microsoftonline.com`. Override for sovereign clouds (e.g. Azure China, US Gov). |

#### Azure Context Banner

When Azure Client Credentials mode is selected, a banner appears at the top of the form showing your signed-in account and a **subscription dropdown**. This dropdown:

- Lists all Azure subscriptions your credential has access to
- Scopes the Key Vault picker to the selected subscription
- Updates the Tenant ID field automatically when you switch subscription
- **Persists** — the selected subscription is saved with the connection and automatically restored when you edit it again

> **tip:** In Docker, set `HOST_AZURE_CONFIG_DIR=~/.azure` in your `.env` file to mount your local `az login` session into the container. See [Environment Variables](../configuration/) for details.

#### Browse App Registrations

Click the **Browse App Registrations…** button next to the Client ID field to open a searchable picker showing all Entra app registrations visible to your credential:

- Search by display name or App ID
- When editing an existing connection, the picker pre-selects and scrolls to the matching app registration
- Selecting an app automatically fills **Client ID** and derives the **Scope** as `api://<firstApiResourceId>/.default`

#### Key Vault Secret Picker

Click **Pick from Key Vault…** next to the Client Secret field to browse your Key Vault secrets without ever seeing the secret value:

**Step 1 — Select a Key Vault**

A searchable list of all Key Vaults in the selected subscription is shown. The vault is filtered by the subscription selected in the Azure Context Banner.

**Step 2 — Select a secret**

All secret names in the chosen vault are listed (names only — values are never retrieved or stored).

The connection stores a `KeyVaultSecretReference` (`vaultName` + `secretName`). At runtime, the API resolves the actual secret value via `DefaultAzureCredential` — the plaintext secret is **never persisted to disk**.

When editing a connection that already has a KV reference, the picker pre-selects the vault and secret automatically.

---

### OAuth

Use OAuth 2.0 PKCE or client-credentials flow for connections that require interactive or automated OAuth authentication.

| Field | Description |
|-------|-------------|
| **Client ID** | Your OAuth client ID. Use the [Browse App Registrations](#browse-app-registrations) button to pick from Entra. |
| **Client Secret** | Optional client secret. Use the [Key Vault picker](#key-vault-secret-picker) to avoid storing it in plain text. |
| **Redirect URI** | The OAuth callback URL registered with your identity provider. |
| **Scopes** | Space-separated list of OAuth scopes to request. |
| **Client Metadata Document URI** | Optional. URL of the OAuth client metadata document (RFC 7591). |

The Azure Context Banner and Key Vault / App Registration pickers are also available in the OAuth section — the same subscription selection and auto-population behaviour applies.
