---
title: "Certificates"
description: "Certificate-based Azure client-credential authentication: create self-signed certs, upload to App Registrations, renew and rotate, Key Vault integration, CSR flow, and CLI."
weight: 15
---

MCP Explorer can authenticate MCP and HTTP connections to Azure using **certificate credentials** instead of client secrets. Certificates are created, stored, uploaded, monitored, and rotated entirely inside the app — the private key never leaves your machine unprotected.

## Why certificates?

Client secrets expire, end up in `.env` files, and get pasted into chats. A certificate credential keeps a private key on disk (never transmitted — token requests are signed locally with `ClientCertificateCredential`), while Azure only ever holds the public key. Microsoft recommends certificate credentials over secrets for production workloads.

## The certificate store

Certificates live under `certs/` in the MCP Explorer data directory (next to `settings.json`, so the same Docker volume persists both):

```
certs/
  .gitignore            # contains "*" — certs can never be committed
  audit.jsonl           # append-only log of every certificate operation
  finance-cert/
    cert.pem            # public certificate
    key.pem             # PKCS#8 private key — file mode 0600, never leaves the machine
    cert.pfx            # optional password-protected bundle
    metadata.json       # subject, thumbprints, expiry, upload records
```

Names are restricted to lowercase letters, digits, and hyphens.

## Creating a certificate

**Inline, while creating a connection** — with Auth Mode `Azure Client Credentials`, switch **Credential Type** from *Client Secret* to *Certificate*, then either pick an existing certificate or expand *Create new certificate…*: subject CN (defaults to `mcp-explorer-{name}`), RSA 2048/4096, validity 6/12/24 months, optional PFX password. Generation uses .NET's built-in crypto (no OpenSSL dependency) and shows step-by-step progress.

**From the Certificates page** — *Certificates* in the Infrastructure sidebar shows stat tiles (total, uploaded, expiring ≤30 days, expired) and a table with thumbprints, expiry status, upload state, and which connections use each certificate. *New Certificate* opens the same generator.

## Uploading to an App Registration

*Upload to App Registration* adds the **public** certificate to the app's key credentials via Microsoft Graph:

1. Load the certificate from the local store
2. Fetch the app registration (requires `az login` or equivalent credentials)
3. Append an `AsymmetricX509Cert` key credential (idempotent — re-uploading the same thumbprint is a no-op)
4. Re-read and verify the thumbprint landed

Your account needs `Application.ReadWrite.OwnedBy` (or Owner on the app registration); a permission failure shows a clear retryable error. After uploading, **Test token acquisition** runs a dry-run `ClientCertificateCredential` flow — newly uploaded certificates can take 30–60 seconds to propagate in Entra ID.

## Expiry monitoring

A background monitor checks on startup and every 12 hours. Certificates expiring within 30 days (or already expired) raise toasts and a topbar badge that links to the Certificates page. The startup pass also verifies uploads against Azure and flags **stale** key credentials (expired in Azure, or belonging to a superseded local certificate).

## Renew & rotate

*Renew & re-upload* rotates a certificate in one click:

1. Generate a successor (same subject and key size; name gets a `-r2`, `-r3`, … suffix)
2. Upload it to every app registration the old certificate was uploaded to
3. Repoint every referencing connection and HTTP API definition
4. Optionally remove the old key credentials from Azure
5. Mark the old certificate *Superseded*

Ordering matters: the old certificate keeps working until everything has succeeded, so a partial failure never breaks existing connections. Stale credentials can also be cleaned up individually via *Manage* on any uploaded app registration.

## Key Vault integration

- **Import from Key Vault** downloads a certificate (with its private key — the vault policy must allow export) into the local store.
- **Export to Key Vault** pushes a local certificate into a vault so teammates can import it.

## CA-issued certificates (CSR flow)

For organisations that require CA-signed certificates: *Create CSR* generates a private key and signing request locally. Download the `.csr`, have your CA issue the certificate, then use the row's *Import issued certificate* action. The issued certificate is validated against the stored private key.

## Export / import

The encrypted connection and HTTP API exports can optionally include referenced certificates (*Include referenced certificates* checkbox). Private keys travel only inside the AES-256-GCM payload (PBKDF2-SHA256, 600k iterations). On import, certificates are written to the local store with `0600` key permissions; existing names are skipped.

## CLI

```bash
mcp-http certs list
mcp-http certs create --name finance-cert --key-size 4096 --validity 24
mcp-http certs upload --name finance-cert --app-id <client-id>
mcp-http certs renew  --name finance-cert --remove-old
mcp-http certs delete --name finance-cert --yes
```

All commands honour `--data-path` and share the same store as the web app.

## Security model

- Private keys are written with file mode `0600` (directories `0700`) and are **never** returned by the API; downloads are public-PEM only, and PFX export always requires a password and is audit-logged.
- Only the public certificate is sent to Microsoft Graph.
- Certificate references (`certificateRef`) stored on connections contain no secret material.
- Deleting a certificate is blocked while any connection or HTTP API definition references it.
- Every create/upload/renew/delete/export operation is appended to `certs/audit.jsonl`.
