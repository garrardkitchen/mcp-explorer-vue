---
title: "App Settings"
description: "Themes, security masking, and data management."
weight: 1
---

## Overview

The Settings page provides global configuration for MCP Explorer — themes, sensitive data protection rules, and data import/export.

![Settings page showing theme switcher and security configuration panels](/images/screenshots/settings-view.png)
*The Settings page groups configuration into cards: Theme, Security, and Data Management.*

---

## Themes

MCP Explorer ships with **10 built-in themes**. Select any theme — your choice is persisted automatically.

| Theme | Style |
|-------|-------|
| **Command Dark** | Dark, high-contrast terminal feel |
| **Command Light** | Clean light counterpart |
| **Nord** | Arctic blue palette |
| **Dracula** | Classic dark purple |
| **Catppuccin Mocha** | Warm dark with pastel accents |
| **Solarized Light** | Warm off-white, reduced glare |
| **GitHub Dark** | Familiar dark mode |
| **GitHub Light** | Classic GitHub light |
| **Material Dark** | Google Material dark |
| **Material Light** | Google Material light |

---

## Sensitive Data Protection

MCP Explorer detects and masks sensitive values using a combination of heuristics and configurable regex patterns.

### Built-in Detection

The following patterns are detected automatically:
- API keys (OpenAI, Anthropic, Azure, AWS, etc.)
- Bearer tokens and JWTs
- Passwords in JSON payloads
- Private key blocks

### Custom Patterns

Add your own regex rules to mask additional sensitive data specific to your environment:

1. Go to **Settings → Security**
2. Click **Add Pattern**
3. Enter a regex pattern (e.g. `sk-[a-zA-Z0-9]{48}`)
4. Optionally give it a name
5. Click **Save**

---

## Data Management

### Export All Data

Export your entire MCP Explorer dataset (connections, models, workflows, settings) to a single encrypted JSON file:

1. Click **Export All Data**
2. Enter an encryption passphrase
3. Download the `.json` file

### Import Data

Restore from an export file:

1. Click **Import Data**
2. Select the `.json` file
3. Enter the passphrase
4. Confirm — existing data is merged

> **warning:** Importing data merges with your existing data. It does not delete existing entries.

---

## Version Info

The bottom of the Settings page shows:
- **API version** — the running ASP.NET Core backend version
- **Frontend version** — the Vue app build version
- **.NET Runtime** — the .NET version in use
