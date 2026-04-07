# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| latest (`main`) | ✅ |
| older tags | ❌ |

We support security fixes on the latest release only. Please upgrade to the latest Docker image tag before reporting.

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub Issues.**

Report vulnerabilities privately via one of:

- **GitHub private vulnerability reporting** - use the [Report a vulnerability](https://github.com/garrardkitchen/mcp-explorer-vue/security/advisories/new) button in the Security tab.
- **Email** - send details to garrardkitchen@gmail.com with subject line `[SECURITY] MCP Explorer - <brief description>`.

### What to include

- Description of the vulnerability and potential impact
- Steps to reproduce or proof-of-concept
- Affected versions / Docker image tags
- Any suggested mitigations

## Response Timeline

| Stage | Target |
|-------|--------|
| Acknowledgement | Within 48 hours |
| Initial assessment | Within 7 days |
| Fix & disclosure | As soon as practical, coordinated with reporter |

## Scope

In scope:
- Docker images (`garrardkitchen/mcp-explorer-x`, `garrardkitchen/mcp-explorer-x-api`, `garrardkitchen/mcp-explorer-x-frontend`)
- API (src/Garrard.Mcp.Explorer.Api)
- Frontend (src/frontend)

Out of scope:
- Vulnerabilities in upstream dependencies (please report those to the respective projects)
- Issues requiring physical access to the host machine
- Social engineering attacks

## Disclosure Policy

We follow a **coordinated disclosure** model. Once a fix is available we will publish a GitHub Security Advisory crediting the reporter (unless they prefer to remain anonymous).
