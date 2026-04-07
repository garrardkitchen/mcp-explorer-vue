# Contributing to MCP Explorer

Thank you for taking the time to contribute! 🎉

## Table of Contents

- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Branching & Commits](#branching--commits)
- [Pull Requests](#pull-requests)
- [Reporting Bugs](#reporting-bugs)
- [Suggesting Features](#suggesting-features)
- [Code Style](#code-style)

---

## Getting Started

1. **Fork** the repository and clone your fork locally.
2. Create a branch from `main` for your change (see [Branching & Commits](#branching--commits)).
3. Make your changes, write or update tests, then open a pull request.

---

## Development Setup

### Prerequisites

| Tool | Version |
|------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ |
| [Node.js](https://nodejs.org/) | 20+ |
| [Docker](https://docs.docker.com/get-docker/) | 20.10+ |

### Backend (API)

```bash
cd src/Garrard.Mcp.Explorer.Api
dotnet restore
dotnet run
```

### Frontend

```bash
cd src/frontend
npm install
npm run dev
```

### Single container (full stack)

```bash
bash run.sh
```

### Tests

```bash
dotnet test --configuration Release --verbosity normal
```

---

## Branching & Commits

Use a short descriptive branch name:

```
feat/add-oauth-support
fix/keyvault-readonly-mount
docs/update-quickstart
```

Follow [Conventional Commits](https://www.conventionalcommits.org/) for commit messages — this drives the automated release notes:

| Prefix | When to use |
|--------|-------------|
| `feat:` | New feature |
| `fix:` | Bug fix |
| `docs:` | Documentation only |
| `refactor:` | Code change that neither fixes a bug nor adds a feature |
| `test:` | Adding or updating tests |
| `chore:` | Build, CI, dependency updates |
| `perf:` | Performance improvement |
| `feat!:` / `fix!:` | Breaking change |

---

## Pull Requests

- Target the `main` branch.
- Keep PRs focused — one logical change per PR.
- Include a clear description of *what* and *why*.
- Add or update tests for any changed behaviour.
- Apply a version label if appropriate (`release_major`, `release_minor`; default is a patch bump):
  - `release_major` — breaking change (bumps X.0.0)
  - `release_minor` — new feature (bumps x.Y.0)
- CI must be green before merge.

---

## Reporting Bugs

Open a [GitHub Issue](https://github.com/garrardkitchen/mcp-explorer-vue/issues/new/choose) and include:

- Steps to reproduce
- Expected vs actual behaviour
- MCP Explorer version / Docker image tag
- Relevant logs (redact any secrets)

---

## Suggesting Features

Open a [GitHub Issue](https://github.com/garrardkitchen/mcp-explorer-vue/issues/new/choose) with the `enhancement` label. Describe the problem you're trying to solve, not just the solution.

---

## Code Style

- **C#** — follow the existing clean-architecture layering (`Core → Application → Infrastructure → Api`). No business logic in controllers.
- **Vue / TypeScript** — Composition API with `<script setup>`. PrimeVue components only; no raw HTML form controls.
- Keep cyclomatic complexity low and functions small.
- Do not commit secrets, credentials, or personal data.
