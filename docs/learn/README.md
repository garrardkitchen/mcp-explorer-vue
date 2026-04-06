# MCP Explorer — Documentation Site

Hugo-based documentation site built with the [Lotus Docs](https://github.com/colinwilson/lotusdocs) theme.

## Local Development

**Prerequisites:** Hugo extended (0.100+), Go 1.21+

```bash
cd docs/learn
hugo mod get          # download theme modules (first time only)
hugo server -D        # start dev server with drafts
```

Open **http://localhost:1313**

## Build

```bash
cd docs/learn
hugo --gc --minify
```

Output is in `docs/learn/public/`.

## Deployment

Automatically deployed to GitHub Pages via `.github/workflows/deploy-docs.yml` on every push to `main` that touches `docs/learn/**`.

Enable GitHub Pages in repo Settings → Pages → Source: **GitHub Actions**.

## Structure

```
content/
├── _index.md                          # Home page (custom layout in layouts/index.html)
└── docs/
    ├── getting-started/               # Quick Start + Configuration
    ├── connections/                   # Managing connections
    ├── tools/                         # Browsing & invoking tools
    ├── prompts/                        # Using prompts
    ├── resources/                     # Browsing resources
    ├── chat/                          # Chat overview
    ├── workflows/                     # Building workflows
    ├── models/                        # Configuring models
    ├── settings/                      # App settings & themes
    └── reference/                     # Keyboard shortcuts + Architecture
```

## Screenshots

App screenshots live in `static/images/screenshots/` and are referenced in content pages.
To refresh screenshots, run the app on `http://localhost:8090` and use Playwright to recapture them.
