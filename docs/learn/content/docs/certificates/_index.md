---
title: "Certificates"
description: "Certificate-based Azure client-credential authentication: create self-signed certs, upload to App Registrations, monitor expiry, renew & rotate, Key Vault integration, CSR flow, and CLI."
weight: 15
---

MCP Explorer can authenticate MCP and HTTP connections to Azure using **certificate credentials** instead of client secrets. Certificates are created, stored, uploaded, monitored, and rotated entirely inside the app — the private key never leaves your machine unprotected.

<img src="/images/screenshots/cert-01-certificates-page.png" alt="Certificates page showing stat tiles and a table of certificates with expiry status, upload state, and used-by counts" style="max-width:900px;border-radius:8px;border:1px solid #e2e8f0;" />

*The Certificates page: stat tiles, expiry status, App Registration upload state, and which connections use each certificate.*

## Why certificates?

Client secrets expire, end up in `.env` files, and get pasted into chats. A certificate credential keeps a private key on disk — token requests are signed locally with `ClientCertificateCredential`, so the key is never transmitted — while Azure only ever holds the public key. Microsoft recommends certificate credentials over secrets for production workloads.

## In this section

- **[Using Certificates]({{< relref "docs/certificates/using-certificates.md" >}})** — create certificates inline while building a connection (or from the Certificates page), upload the public key to an App Registration in one click, and dry-run token acquisition.
- **[Renewal & Monitoring]({{< relref "docs/certificates/renewal-and-monitoring.md" >}})** — expiry toasts and topbar badge, one-click renew & rotate, and stale key-credential cleanup.
- **[Key Vault, CSR, Export & CLI]({{< relref "docs/certificates/sharing-and-automation.md" >}})** — share certificates via Azure Key Vault or encrypted exports, use CA-issued certificates via the CSR flow, and automate everything with `mcp-http certs`.

## The certificate store

Certificates live under `certs/` in the MCP Explorer data directory (next to `settings.json`, so the same Docker volume persists both):

```
certs/
  .gitignore            # contains "*" — certs can never be committed
  audit.jsonl           # append-only log of every certificate operation
  finance-api-cert/
    cert.pem            # public certificate
    key.pem             # PKCS#8 private key — file mode 0600, never leaves the machine
    cert.pfx            # optional password-protected bundle
    metadata.json       # subject, thumbprints, expiry, upload records
```

Names are restricted to lowercase letters, digits, and hyphens. Generation uses .NET's built-in cryptography — no OpenSSL dependency, identical behaviour on macOS, Windows, Linux, and in Docker.

## Security model

- Private keys are written with file mode `0600` (directories `0700`) and are **never** returned by the API; downloads are public-PEM only, and PFX export always requires a password and is audit-logged.
- Only the public certificate is sent to Microsoft Graph.
- Certificate references stored on connections (`certificateRef`) contain no secret material.
- Deleting a certificate is blocked while any connection or HTTP API definition references it.
- Every create / upload / renew / delete / export operation is appended to `certs/audit.jsonl`.
