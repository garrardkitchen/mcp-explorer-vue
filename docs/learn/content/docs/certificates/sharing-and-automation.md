---
title: "Key Vault, CSR, Export & CLI"
description: "Share certificates via Azure Key Vault or encrypted exports, bring CA-issued certificates with the CSR flow, and automate everything with the mcp-http certs CLI."
weight: 3
---

## Azure Key Vault integration

Key Vault is the natural way for a team to share a certificate without emailing private key files around:

- **Import from Key Vault** (Certificates page header) — pick a vault, pick a certificate, give it a local name. The certificate is downloaded *with its private key* into the local store — the vault certificate's policy must allow an **exportable** private key.
- **Export to Key Vault** (detail row) — pushes a local certificate (as PFX) into a vault so teammates can import it into their own MCP Explorer.

Both use the same `DefaultAzureCredential` chain as the rest of the app (`az login` locally, environment/managed identity in containers). Vault certificate names are listed via `GET /api/v1/azure/keyvaults/{vault}/certificates`.

## CA-issued certificates (CSR flow)

Self-signed certificates are fine for Entra ID client-credential auth, but some organisations require certificates issued by an internal CA:

1. **Create CSR** (Certificates page header) — generates a private key and a certificate signing request locally. The entry appears as **CSR pending**; the private key never leaves the machine.
2. **Download CSR** (row action) — send the `.csr` file to your certificate authority.
3. **Import issued certificate** (row action) — paste the PEM your CA returns. It is validated against the stored private key before being accepted, and the entry becomes **Active**.

Pending CSRs are excluded from the connection dialogs' certificate picker until issued.

## Certificates in encrypted exports

The existing password-protected connection and HTTP API exports can carry certificates too: tick **Include referenced certificates** in either export dialog. Any certificate referenced by a selected connection/definition is bundled — private keys travel **only inside** the AES-256-GCM encrypted payload (PBKDF2-SHA256, 600k iterations).

On import, bundled certificates are written into the local store with `0600` key permissions; a certificate whose name already exists is skipped (the import summary reports both counts). Exports made without the checkbox keep the original file format, so older builds can still import them.

## CLI: `mcp-http certs`

Everything above is scriptable. The CLI shares the same store and services as the web app (point it at the same data dir with `--data-path`):

```bash
# List certificates with expiry and upload state
mcp-http certs list

# Generate a self-signed certificate
mcp-http certs create --name finance-api-cert --key-size 4096 --validity 24

# Upload the public key to an App Registration
mcp-http certs upload --name finance-api-cert --app-id 00000000-0000-0000-0000-000000000000

# Rotate: successor + re-upload + repoint connections (+ optional cleanup)
mcp-http certs renew --name finance-api-cert --remove-old

# Delete (blocked while referenced — prints the referencing connections)
mcp-http certs delete --name finance-api-cert --yes
```

`create`, `upload`, and `renew` print the same step-by-step progress as the UI (`✓` / `✗` / `→` per step) and exit non-zero on failure, so they slot straight into CI pipelines.
