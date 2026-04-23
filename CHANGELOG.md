# Changelog

## [Unreleased] - 2026-04-23

### Added
- **Docs — Dev Tunnels section**: Added new `dev-tunnels/` documentation section with `_index.md` landing page and a full `using-dev-tunnels.md` page covering tunnel management, Signal Deck dashboard, live event inspection, webhook replay, CLI unavailable state, and tool smart-fill integration. Includes 12 Playwright-captured screenshots (`dt-01` through `dt-09`, plus copy-URL toasts and webhook chip popover) covering the full feature workflow.
- **Docs — Dev Tunnels killer feature**: Added Dev Tunnels as feature #12 ("Webhook Signal Deck") on the documentation homepage, updated the section title from "11 Killer Features" to "12 Killer Features", added a 🚇 Dev Tunnels hero-meta badge, and highlighted the card with a "NEW" badge and green accent styling.
- **Docs — `HOST_DEVTUNNELS_DIR` environment variable**: Added documentation for the `HOST_DEVTUNNELS_DIR` variable to `reference/environment-variables.md` (new "Dev Tunnels" subsection with per-OS paths and a Quick Reference snippet), `getting-started/quickstart.md` (Option 2 `.env` snippet and embedded `docker-compose.yml` updated with missing `devtunnels-cli` volume mount), and `dev-tunnels/using-dev-tunnels.md` (tip in the "Login Required" section linking to the new reference).

### Fixed
- **Docs — Dev Tunnels accuracy**: Corrected device-code login dialog description (auto-starts on open; no manual button click required); updated smart-fill picker to state Running *or* Starting tunnels are eligible; expanded SSRF blocklist to include `0.x.x.x` unspecified addresses and IPv6 ULA (`fc00::/7`). Also corrected smart-fill field patterns to document all four matches: `webhookUrl`, `callbackUrl`, `webhook`, `callback`.
- **Docs — Homepage Quick Start layout**: Changed steps grid from `auto-fill` to `auto-fit` so the four step cards always expand to fill the full section width at all viewport sizes, eliminating the ghost column that caused cards to left-align on wider screens.

### Fixed
- **Dev Tunnels — inspector card grid placement**: The inspector `Card` was silently placed in the grid's `auto` row whenever `tunnel.lastError` was falsy and the error `<Message>` was not rendered, causing the card to collapse to content height and hide the Traffic/Archive tabs. Fixed by pinning `.inspector-shell` to `grid-row: 3` so it always occupies the `minmax(0, 1fr)` row regardless of whether the error banner is present.

## [Unreleased] - 2026-04-22

### Changed
- **Dev Tunnels — unified inspector hero**: The compact hero header (tunnel name, webhook/tunnel URL fields, metrics, and action buttons) is now identical on both the Traffic and Archive tabs. Removed the conditional `isTrafficMode` flag and `traffic-mode` CSS modifier that previously showed a larger hero on Archive and a smaller one on Traffic.
- **Dev Tunnels — traffic feed height fix**: Replaced the PrimeVue `ScrollPanel` wrapper in the event feed with a plain flex `div` (`overflow-y: auto`) so incoming events fill all available vertical space to the bottom of the page. Also carried a proper `flex: 1 1 0 / min-height: 0` chain through the PrimeVue Card body, card content, Tabs, and active TabPanel so no height is silently consumed by intermediate containers. The Listbox list container is set to `overflow: visible` to prevent a nested inner scroll.
- **Dev Tunnels — signal rail typography**: Replaced the serif (`Iowan Old Style / Palatino / Georgia`) font used on the "Pulse window", "Playback rail", and "Selected frame" card headings in the analytics rail with the app's inherited sans-serif body font (`font-weight: 600`).


- **Dev Tunnels — traffic tab Split Signal Desk**: Reworked the Traffic tab around a compact traffic-only hero, a dedicated left analytics rail for pulse/playback/current-frame context, and a dominant right-hand live event feed so incoming traffic stays visible within the viewport on desktop layouts.
- **Dev Tunnels — inspector root scrolling**: Stopped the inspector page itself from acting as the scroll container and tightened the main inspector card's shrink behavior so switching to Archive can no longer reveal page background by scrolling past the workspace.
- **Dev Tunnels — archive overflow cleanup**: Removed leftover archive-pane `height: 100%` / `min-height: 100%` forcing rules and hid outer overflow so the Archive tab no longer generates phantom page scroll after switching tabs and scrolling downward.
- **Dev Tunnels — archive tab stretch fix**: Made the active inspector tab panel a real flex container and wrapped the archive workspace in an explicit fill panel so the Archive tab uses the same full-height card area as Traffic instead of collapsing to its content height.
- **App shell — non-blocking Dev Tunnels hydration**: Moved Dev Tunnels list hydration out of the critical startup `Promise.all` so slow or unavailable tunnel APIs cannot delay the rest of the app from becoming interactive.
- **Dev Tunnels — inspector height chain**: Wrapped routed pages in explicit stretch hosts and tightened the inspector root height chain so the Dev Tunnels inspector card occupies the available app viewport instead of leaving unused background space beneath the page.
- **Dev Tunnels — archive paging follow-up**: Let the captured-events list fill its archive pane again, made manual paginator navigation select an event on the newly chosen page, and fixed page clamping so the list contents and PrimeVue page report cannot drift out of sync.
- **Dev Tunnels — PrimeVue visual polish**: Replaced the remaining hand-built dashboard/inspector chrome with PrimeVue-backed cards, toolbars, listboxes, sliders, splitters, messages, and readonly input fields so the new Dev Tunnels pages now align with the rest of the app's component language.
- **Dev Tunnels — histogram refresh**: Replaced the bar-style traffic histograms with a shared sparkline/area graphic that reads more cleanly across the dashboard, live traffic panel, and playback rail. Empty tunnels now render a dedicated "waiting for traffic" placeholder instead of a wall of dead bars.
- **Dev Tunnels — layout cleanup**: Removed the redundant dashboard registry header, moved webhook/tunnel copy actions into the URL fields themselves, shrank the inspector traffic/playback graphs, flattened the archive workspace so the splitter does the framing, and rebalanced the captured/latest/unseen metric tiles for better spacing.
- **Dev Tunnels — archive alignment cleanup**: Made the archive event list and selected frame workspace fill their splitter panes from the top so captured events and frame details use the full available width instead of floating mid-panel.
- **Dev Tunnels — sample curl shortcut**: Added a webhook field action on the dashboard and inspector that copies a ready-to-run sample `curl` command using the active webhook URL plus a generated JSON body containing a nested `user`, an ISO `datetime`, and `event_type: "test"`.
- **Dev Tunnels — archive tab fix**: Restored reactive tab switching in the archive detail workspace so Body, Headers, and Raw can all be selected again.
- **Dev Tunnels — login cancel cleanup**: Closing the connect dialog now cancels the live device-code SSE stream and clears any queued auto-start tunnel so dismissing the login flow cannot unexpectedly finish and launch a tunnel later.
- **Dev Tunnels — archive height fill**: Forced the archive tab, splitter panes, and captured-events list viewport to stretch to the available vertical space so the left-hand archive column fills the inspector pane instead of collapsing short.
- **Dev Tunnels — archive paging**: Reworked the archive list to use a bounded viewport with PrimeVue pagination instead of an unbounded full-height event stack, keeping captured frames readable while still letting the right-hand detail workspace use the remaining space.

## [Unreleased] - 2026-04-21

### Fixed
- **Dev Tunnels — external webhook POSTs returned HTTP 502**: The port was registered with `--protocol https`, but the API container's Kestrel listener serves plain HTTP on `:5000`. Microsoft's relay therefore attempted a TLS handshake against a plain-HTTP socket and every inbound request failed at the relay with `502 Bad Gateway`. The CLI wrapper now registers the port with `--protocol http` (the relay still exposes the tunnel over HTTPS publicly and terminates TLS before forwarding). To also heal tunnels that were previously registered with the wrong protocol, every start now runs `devtunnel port delete` before `port create`, so the port record gets refreshed on each host.
- **Dev Tunnels — status stuck on "Starting"**: `InferHostStatus` now treats "Ready to accept connections" and "Hosting port" CLI lines as `Running`, and `MonitorHostAsync` no longer downgrades an already-`Running` tunnel back to `Starting` when later unrecognised host output lines arrive. This unblocks the inspector/list UI (webhook + tunnel URL copy buttons are now enabled as soon as the CLI reports the URL).
- **Dev Tunnels — UI status never refreshed after Start**: Added a 1.5s per-tunnel poll in the `devTunnels` Pinia store that runs while a tunnel is in `Starting` and tears itself down as soon as it settles, so the list and inspector views reliably transition to `Running` with populated webhook/tunnel URIs without a manual refresh.

### Added
- **Dev Tunnels**: Add a full Dev Tunnel workflow to MCP Explorer — create/start/stop/delete tunnel definitions, stream `devtunnel` device-code login instructions, expose webhook capture endpoints, persist captured events, replay stored webhook payloads, and browse a new live inspector with SSE tape, timeline scrubber, and event detail panels.
- **Tools integration**: Tool parameter forms now offer a one-click "Use active tunnel" picker for webhook-style fields such as `webhookUrl` and `callbackUrl`.

### Changed
- **Dev Tunnels — Signal Deck redesign**: Reworked both Dev Tunnels pages around a wider "Signal Deck" layout. The dashboard now uses a stronger display-heading system, larger hero summaries, per-tunnel captured-event totals, per-card mini histograms, unseen-event counts, and a cleaner card layout that makes better use of widescreen space.
- **Dev Tunnels — inspector tab workspace**: The inspector now separates **Traffic** and **Archive** into top-level tabs. Incoming traffic is rendered in a dedicated scrollable live tape with event selection, while captured history and the selected frame live together in the Archive workspace.
- **Dev Tunnels — global webhook notifications**: Running tunnel streams now stay alive across the app shell so webhook arrivals can raise off-page toast notifications. Added a pause/resume control in the top bar plus unseen-event counting so background activity is still visible even when notifications are muted.
- **Dev Tunnels — service-generated tunnel IDs**: The CLI wrapper now lets the Dev Tunnels service mint tunnel IDs (e.g. `fun-plane-vc21wbv`) instead of passing our own short names like `foo` as positional arguments to `devtunnel create`. The service-assigned ID is persisted on the local tunnel record as `CliTunnelId` and reused for every subsequent `host` / `delete`. Short custom IDs could silently collide with phantom/stale service records, producing "Tunnel not found" errors on start even when `devtunnel list` showed them — this eliminates that entire class of failure.
- **Dev Tunnels — credential volume path**: Docker Compose now mounts the CLI credential volume at `/root/.local/share/DevTunnels` (where the current `devtunnel` CLI actually writes tokens), not the legacy `/root/.devtunnels` path which was always empty. This finally makes a one-time login stick across `docker compose down -v`.
- **Dev Tunnels — unified create/host flow**: Replaced the ephemeral `devtunnel host -p <port> -a` path (which silently produced no URL in non-TTY containers because the port was never actually registered) with a persistent `create → port create → host` flow for both anonymous and authenticated tunnels. Anonymous tunnels pass `--allow-anonymous` at create time; the flow otherwise looks identical. This is the only reliable way to get the service to emit the `Connect via browser:` URL line the UI parses for webhook/tunnel URLs.
- **Dev Tunnels — one-time sign-in helper**: Added `scripts/devtunnel-login.sh` and a dedicated section in `DOCKER_README.md` so users can complete the (unavoidable) Microsoft Dev Tunnels host-side sign-in once — future tunnel starts from the UI then require zero interaction. The in-app device-code dialog remains as a fallback.
- **Dev Tunnels — seamless Connect flow**: Clicking **Start** on a tunnel now opens an in-app *Connect to Dev Tunnels* dialog that parses the device code + verification URL from the CLI, renders click-to-open / click-to-copy chips, and — on completion — automatically resumes the tunnel you were starting. The old "CLI login" toolbar button and amber login banner are gone; the concept of "logging in" is abstracted away.
- **Dev Tunnels — reuse host login**: Added `HOST_DEVTUNNELS_DIR` env var which, when set, mounts the host's existing DevTunnels credentials directory into the container at `/root/.devtunnels`. This lets the container inherit an existing `devtunnel user login` session so you never see the "login required" prompt. Paths differ per OS — see `.env.example`.
- **Dev Tunnels — anonymous flow**: Anonymous tunnels now use the one-shot `devtunnel host -p <port> --protocol https -a` command (inspired by `cloud-share`). The `-a` flag lets **callers** connect without auth — the CLI host still has to be signed in (the tunnel service requires "create" scope on the host). The Docker Compose file now mounts a `devtunnels-cli` volume at `/root/.devtunnels` so the sign-in token persists across container restarts.
- **Host-failure diagnostics**: When `devtunnel host` fails with an auth/scope error (e.g. `Unauthorized … does not have 'create' access scope`), the tunnel is moved to `LoginRequired` with the underlying message surfaced, instead of crashing as a generic Error.
- **App shell**: Add a top-level Dev Tunnels navigation section and a live tunnel count badge in the header.

### Fixed
- **Dev Tunnels — copy buttons**: The **Copy webhook** / **Copy tunnel URL** buttons now work in non-secure contexts (LAN IP access) via a `document.execCommand('copy')` fallback, and also log the URL to the browser console and API logs so you can retrieve it from either place even when clipboard access is denied.
- **Dev Tunnels — URL parsing regex**: Tightened the `devtunnels.ms` URL regex to stop at commas so the CLI's `Connect via browser: <url1>, <url2>` output no longer trails the comma into the captured URI.
- **Dev Tunnels UI — show tunnel URL**: Tunnel cards now display the base tunnel URL prominently alongside the webhook target, with a dedicated "Copy tunnel URL" action (the existing copy button is now "Copy webhook").
- **Dev Tunnel host lifetime**: Hosted `devtunnel host` processes no longer inherit the start HTTP request's `CancellationToken`. Kestrel's `RequestAborted` token was firing after the start response completed, which cancelled the hosted tunnel prematurely and caused tunnels to show as "Stopped" when users navigated back to the Dev Tunnels view. The hosted runtime now uses a standalone `CancellationTokenSource` that only cancels on explicit stop or shutdown.
- **Dev Tunnels UI — stream reconnect**: When returning to the Dev Tunnels view, live event streams are now automatically reconnected for any tunnels the server reports as Running or Starting (`store.ensureStreamsForRunning()`), so the "· Live" indicator and incoming events resume without requiring a manual restart.
- **Dev Tunnel hardening**: Reject replay targets that are not public HTTP(S) addresses, pin replay connections to approved resolved IPs, reject webhook payloads that exceed the configured capture limit, dispose hosted tunnel cancellation state cleanly, and honor `DeleteOnExit` tunnel cleanup during shutdown.
- **Dev Tunnel CLI in Docker**: Install the `devtunnel` binary in both runtime images, return structured `503` responses when the CLI is unavailable, and surface that unavailable state clearly in the Dev Tunnels UI instead of crashing login/create requests.

## [Unreleased] - 2026-04-08

### Added
- **README**: Full rewrite — badges row (CI/CD, License, Docker Hub), Blazor rewrite callout linking original repo, expanded features list (Azure Key Vault, Entra App Registrations, Elicitations, Import/Export), corrected transport to Streamable HTTP only (removed SSE/stdio), architecture split into 6 focused mermaid diagrams (System Overview, Deployment Modes, Clean Architecture, Azure Integration, Elicitation Flow, Security & Data Protection), new Azure Setup section with RBAC roles and Graph API permissions tables, corrected Docker run command matching `run.sh`, `HOST_AZURE_CONFIG_DIR` and `AZURE_CONFIG_DIR` env var rows, `/api/v1/azure` API row, updated project layout with `Azure/KeyVaultService.cs` and `Azure/GraphService.cs`, new Community table, updated Contributing steps with Conventional Commits.

- **CI/CD — GitHub Release**: `release` job now generates conventional-commit release notes (grouped by `feat:`, `fix:`, `docs:`, `perf:`, `refactor:`, `test:`, `chore:`, `feat!:`/`fix!:` breaking changes) and publishes a GitHub Release via `gh release create`. `last_tag` exposed as a second output from `determine-version` so the release job can compute the correct diff range.
- **CI/CD pipeline**: Added `.github/workflows/ci-cd.yml` with four jobs — `test` (PR coverage reporting), `determine-version` (semantic versioning from PR labels: `release_major`/`release_minor`/default patch), `build-push` (parallel matrix build of `mcp-explorer-x`, `mcp-explorer-x-api`, `mcp-explorer-x-frontend` for `linux/amd64` + `linux/arm64`), and `release` (git tag + DockerHub description sync). All jobs have explicit `if:` guards. Added `APP_VERSION` and `VITE_API_BASE_URL` build-arg support to all three Dockerfiles; both VITE vars now correctly wired into `npm run build` in the single-container and standalone frontend Dockerfiles. Added `DOCKER_README.md` (DockerHub-friendly, mermaid-free, complete env var reference).
- **Docs — Azure Client Credentials screenshot**: Adds masked screenshot of the Edit Connection dialog (Azure Client Credentials mode) to `managing-connections.md`, with all sensitive fields (tenant ID, client ID, endpoint, scope, KV ref) redacted.
- **Docs — Azure-Powered Authentication feature card**: Added new card 4 (`feature-card--highlight`) on the landing page showcasing Azure Key Vault + Entra App Registration credential browsing; existing cards 4–10 renumbered to 5–11. Section header updated to "11 Killer Features". Hero meta updated with `🔐 Azure KV` span.
- **Docs — Authentication Modes reference**: `managing-connections.md` now documents all three auth modes (Custom Headers, Azure Client Credentials, OAuth) including the Azure Context Banner, Browse App Registrations picker, and Key Vault Secret Picker.
- **Docs — Azure volume mount in quickstart**: `quickstart.md` docker-compose.yml block updated with `AZURE_CONFIG_DIR` env var, `HOST_AZURE_CONFIG_DIR` volume mount, and `azure-config-empty` fallback volume.
- **Docs — Azure Integration environment variables**: `environment-variables.md` now includes a `### Azure Integration` subsection documenting `HOST_AZURE_CONFIG_DIR` and `AZURE_CONFIG_DIR`, plus a new `## Azure Integration Quick Reference` section at the end.

- **Azure subscription picker**: Connections page now shows a subscription dropdown in the Azure Context Banner when multiple subscriptions are available; changing the selection propagates to `KeyVaultSecretPicker` so vaults are scoped to the chosen subscription. Backend exposes `GET /api/v1/azure/subscriptions` and `GET /api/v1/azure/keyvaults?subscriptionId=` accordingly.
- **Subscription persistence**: The selected Azure subscription is now persisted in `AzureClientCredentialsOptions.SubscriptionId` alongside the connection — it is automatically restored when the connection is edited again.
- **Azure CLI in Docker**: `Dockerfile.api` now installs `azure-cli` in the final stage so `AzureCliCredential` can call `az account get-access-token` inside the container.
- **Azure credential log noise eliminated**: `AzureContextService` now routes `CredentialUnavailableException` to `Debug` instead of `Warning` — the full stack trace no longer appears in production logs when running without Azure credentials. `KeyVaultSecretResolver` does the same and wraps it in a clear user-facing message.

### Fixed
- **OAuth Key Vault secret not resolved** — `BuildOAuthOptions` was using the plaintext `ClientSecret` directly; refactored to `BuildOAuthOptionsAsync` so `KeyVaultSecretRef` is resolved via `IKeyVaultSecretResolver` before the OAuth flow begins.
- **`KeyVaultSecretRef` dropped on save/load** — `PreferencesMapper` encrypt and decrypt paths now round-trip `KeyVaultSecretRef` for both `AzureClientCredentialsOptions` and `OAuthConnectionOptions` (the reference is never encrypted — it contains no secret value).
- **SSRF / vault name injection** — Vault names are now validated against Azure naming rules (`^[a-zA-Z][a-zA-Z0-9\-]{1,22}[a-zA-Z0-9]$`) in both `AzureController.GetKeyVaultSecrets` and `KeyVaultSecretResolver.ResolveAsync` before the URI is constructed.
- **`OperationCanceledException` swallowed** — `KeyVaultSecretResolver` now re-throws `OperationCanceledException` directly instead of wrapping it in `InvalidOperationException`.
- **Key Vault picker blank on edit** — `KeyVaultSecretPicker.open()` now pre-selects the vault matching the current `modelValue`; `goToStep2` pre-selects the matching secret name after the list loads.

## [Unreleased] - 2026-04-07 (latest)

### Added
- **Azure Key Vault integration**: Connection form now supports resolving client secrets from Azure Key Vault at runtime via `DefaultAzureCredential` (AzureCliCredential → EnvironmentCredential → VisualStudioCredential). Secrets are stored as vault/secret references — never persisted as plain text.
- **Azure Assist (Design 2)**: Live Azure Context Banner on Azure Client Credentials and OAuth sections shows active account, subscription, and connection status. Includes Browse App Registrations picker (searchable, auto-fills Client ID + Scope) and two-step Key Vault Secret Picker (vault → secret).
- **Tenant ID auto-population**: Tenant ID is auto-populated from `az account show` when the form opens; manual override is retained.
- **Scope auto-population**: Selecting an App Registration automatically derives the scope as `api://<resourceAppId>/.default`.
- **New backend API endpoints**: `GET /api/v1/azure/account`, `GET /api/v1/azure/app-registrations`, `GET /api/v1/azure/keyvaults`, `GET /api/v1/azure/keyvaults/{vaultName}/secrets`.
- **New components**: `AzureContextBanner.vue`, `AppRegistrationPicker.vue`, `KeyVaultSecretPicker.vue`.
- **New unit tests**: `KeyVaultSecretResolverTests`, `KeyVaultSecretReferenceTests`, `ConnectionOptionsKeyVaultTests` (15 tests added, total 147).
- **Quick Start docs**: Rewrote quick start page with OS-tabbed `docker run` examples (macOS, Ubuntu, Windows), `.env` setup instructions, and full `docker-compose.yml` inline reference.
- **Connections docs**: Added Add Group dialog screenshot (`48-connections-add-group.png`) to the Grouping Connections section.

### Changed
- `AzureClientCredentialsOptions`: Added `KeyVaultSecretRef` property (`KeyVaultSecretReference?`).
- `OAuthConnectionOptions`: Added `KeyVaultSecretRef` property (`KeyVaultSecretReference?`).
- `ConnectionService`: Injects `IKeyVaultSecretResolver`; resolves KV reference before acquiring Azure access token.

---

## [Unreleased] - 2026-04-07

### Fixed
- **Transport type corrections**: MCP Explorer only supports Streamable HTTP transport. Removed all incorrect references to `stdio`, `SSE`, and multi-transport from: connections docs, architecture diagram, configuration docs, landing page quick-start, landing page feature card, README features list, README architecture diagram.

---

## [Unreleased] - 2026-04-06 (latest)

### Changed
- **README**: Rename app from "MCP Explorer v2" to "MCP Explorer". Add link to Hugo docs site. Correct test counts (Core: 66, Infrastructure: 47, Api: 34, total: 147). Update Themes section from 6 to 10 themes with full table. Add `docs/learn/` to project layout. Rewrite Environment Variables table to match actual `.env.example` (removed stale `LLM__*` vars, added all current vars with defaults).

### Added
- **Workflow documentation** (`docs/learn/content/docs/workflows/building-workflows.md`): Full step-by-step walkthrough with 11 Playwright screenshots covering workflow creation, PromptAtRuntime parameter mapping, FromPreviousStep chaining, runtime parameter dialog, execution results, and history view.
- **Data Guard documentation** (`docs/learn/content/docs/settings/data-guard.md`): Covers all three detection layers (regex, heuristic entropy, AI-powered), strictness levels, custom patterns, and allowed fields bypass list. Includes 2 screenshots.
- **Chat documentation** (`docs/learn/content/docs/chat/chat-overview.md`): Full rewrite with 5 screenshots covering connection selection, message send, tool-calling response, slash command palette, and Prompt Picker dialog.
- **App rename in docs**: All documentation pages updated from "MCP Explorer X" to "MCP Explorer".

---

## [Unreleased] - 2026-04-06

### Added
- **Hugo documentation site** (`docs/learn/`): Full documentation site built with Hugo and Lotus Docs theme. Covers Getting Started, Connections, Tools, Prompts, Resources, Chat, Workflows, Models, Settings, and Reference sections.
- **Landing page**: Custom homepage with hero section, 10 killer features grid, quick-start steps, screenshot gallery, and CTA section. Indigo/purple brand palette matching the app theme.
- **Live screenshots**: App screenshots captured from running app at `http://localhost:8090` using Playwright and embedded in documentation pages.
- **GitHub Actions workflow** (`.github/workflows/deploy-docs.yml`): Automatically builds and deploys docs to GitHub Pages on push to `main`.
- **Architecture docs**: Mermaid diagrams in `docs/learn/content/docs/reference/architecture.md` showing single-container and docker-compose deployment modes and data flow sequence diagram.

---

## 2026-04-06

### Added
- **MCP User-Agent header**: All outbound MCP tool calls now send `User-Agent: mcp-explorer/<version>` (e.g. `mcp-explorer/1.0.0`). Version is read at runtime from the entry assembly's informational version so it stays in sync automatically. Both initial connections and sampling reconnects use a pre-configured `HttpClient` passed to `HttpClientTransport`.
- **Hostname in User-Agent**: `Dns.GetHostName()` appended as a comment — e.g. `mcp-explorer/1.0.0 (my-hostname)`.
- **`MCP_CLIENT_NAME` env var**: Override the client name sent during MCP handshake and in User-Agent. Defaults to `mcp-explorer`. Documented in `.env.example` and `docker-compose.yml`.
- **`ClientVersion` dynamic**: Infrastructure project no longer uses a hardcoded `"0.5.0"` const — version is read from the entry assembly at runtime, keeping `ClientInfo.Version`, `User-Agent`, and `ConnectionContext` in sync.
- **Tools favourites filter persisted**: `showFavoritesFirst` toggle in the Tools view now calls `preferencesApi.patch()` on change (was previously only toggling local state). Consistent with Prompts, Resources, and Templates views.
- **`features.md`**: Comprehensive feature reference document listing all capabilities grouped by navigation section.

## [Unreleased] - 2026-04-05

### Added
- **Encrypted connection export/import**: Export now opens a dialog where you select individual connections (filterable by name/endpoint), enter a password and confirm it, then download an encrypted `.json` file. Import opens a dedicated dialog with a drag-and-drop file zone and a password field; if the password doesn't match, the API returns a clear error. On name collision the imported connection is renamed `(v2)`, `(v3)`, etc. Encryption uses AES-256-GCM with PBKDF2-SHA256 key derivation (100,000 iterations). New `IConnectionExportService` / `ConnectionExportService` in Core/Infrastructure; 16 new unit tests covering round-trips, wrong-password rejection, tamper detection, and name-collision versioning.

### Fixed
- Connections export returned 405 Method Not Allowed. `GET /connections/export` and `POST /connections/import` endpoints were missing from `ConnectionsController`. Added both: export returns a downloadable `connections.json` file; import accepts a JSON array of connection definitions and merges them in, skipping any whose name already exists and returning `{ imported, skipped }` counts. Updated `connectionsApi` helper signatures to match.

### Changed
- Prompts page: "Send to LLM" no longer navigates away to the Chat page. The result panel is now a two-tab panel: **JSON Result** tab (auto-focused on Execute) and **LLM Response** tab (auto-focused on Send to LLM). The model picker is now an inline popover triggered from a `✦ Send to LLM` button in the action bar — no full-screen dialog. The LLM response streams in real-time with a blinking cursor indicator and animated dot on the tab. A **Cancel** button appears during streaming. The response is rendered as DOMPurify-sanitised markdown. The LLM session is created in the background; streaming is cleanly cancelled on re-execute, prompt change, or page unmount. The args section caps at 40% height with overflow-scroll to prevent crowding when a prompt has many arguments.

### Added
- Favourites feature extended to Prompts, Resources, and Resource Templates pages. Each page now supports: per-item ⭐ star button to toggle favourites (persisted via preferences API), a ⭐ star toggle in the panel header to group favourites first with visual separator rows, and a count badge that shows filtered/total when a search is active. Backend: added `FavoriteResourceTemplates` and `ShowResourceTemplateFavoritesFirst` to `UserPreferences` domain model; `PatchPreferencesRequest` now exposes all six `ShowXxxFavoritesFirst` flags for prompts, resources, and templates. Preferences are loaded on mount and persisted on every toggle.
- Markdown documentation dialog extended to Prompts, Resources, and Resource Templates pages. Each page now has: (1) a 📖 book icon button in the list panel header to open a reference doc for all visible items, and (2) a 📖 book icon button in the detail panel to open docs for the selected item. Generates well-structured markdown with argument/parameter tables, URI info, MIME type, and anchor navigation. `ToolDocsDialog` now accepts `rawMarkdown` and `title` props to support non-tool content. New generators added to `useToolDocs.ts`: `generatePromptMarkdown`, `generatePromptsListMarkdown`, `generateResourceMarkdown`, `generateResourcesListMarkdown`, `generateResourceTemplateMarkdown`, `generateResourceTemplatesListMarkdown`.

### Fixed
- Markdown documentation dialog (all-tools mode): scroll now works correctly when the dialog is maximised. Two root causes resolved: (1) the Dialog `:style` binding applied `max-height: 85vh; width: 780px` as inline styles even in maximised mode — inline styles override PrimeVue's class-based maximize CSS, capping the dialog at 85 vh; fixed by making `:style` conditional on `isMaximized`. (2) PrimeVue's own `.p-tabpanel` stylesheet sets `display: block; flex: 0 1 auto` which overrides the scoped `flex: 1; min-height: 0` chain; fixed with higher-specificity global overrides on `.docs-dialog .p-tabpanel-active` and `.docs-dialog .p-tabpanels`.

## [Unreleased] - 2026-04-04

### Added
- Two additional light themes: **Material Light** (Material Design 3 light scheme — deep violet `#6750a4` accent, M3 tonal surface container layers, `#fffbfe` base) and **GitHub Light** (GitHub Primer light palette — GitHub blue `#0969da` accent, exact canvas/border values from github.com `#ffffff` base). The theme switcher now offers 10 themes total (6 dark, 4 light).

### Added
- Two new themes: **Material Dark** (Material Design 3 dark scheme — tonal violet `#d0bcff` accent, M3 tonal surface layers) and **GitHub Dark** (GitHub Primer dark palette — GitHub blue `#58a6ff` accent, exact canvas/border values from github.com). Both themes are dark mode and appear in the theme switcher alongside the existing 6 themes.

### Added
- Elicitation dialog now renders **radio buttons** for single-select enum schemas (`UntitledSingleSelectEnumSchema` → `{type:"string",enum:[...]}`, `TitledSingleSelectEnumSchema` → `{type:"string",oneOf:[{const,title}]}`) and **checkboxes** for multi-select schemas (`UntitledMultiSelectEnumSchema` → `{type:"array",items:{enum:[...]}}`, `TitledMultiSelectEnumSchema` → `{type:"array",items:{anyOf:[{const,title}]}}`). Options render as styled interactive cards with a highlighted border and custom radio/check indicator when selected. Multi-select values are submitted as a JSON array.

### Fixed
- Elicitation dialog now correctly renders all schema fields (boolean toggle, enum dropdown, number input, date picker, string input). Root cause was two bugs: (1) schema property objects were stored as `Dictionary<string, object>` causing STJ to emit `{}` for each field due to polymorphic boxing — fixed by serializing each property using its concrete runtime type via `JsonSerializer.SerializeToElement`; (2) `ElicitationDialog.vue` was looking for `schema.properties` but the backend sends properties as the schema root — fixed to iterate the schema map directly
- Elicitation number fields now correctly coerce the HTML string input value to a number before sending to the backend (MCP server calls `GetInt32()`/`GetDouble()` on the `JsonElement`)
- Elicitation date/datetime fields now render a native `<input type="date">` / `<input type="datetime-local">` instead of a plain text field
- Type-pill in field rows now shows the format when present (e.g. `string (date)`)

### Added
- Elicitation dialogs now appear directly inside the Tools page when the MCP server requests user input mid-tool-call
- `useElicitation` composable (`src/composables/useElicitation.ts`): opens an SSE stream scoped to the selected connection; maintains a FIFO queue so multi-step elicitation flows (multiple consecutive requests) are handled one at a time; automatically reopens stream when the selected connection changes
- `ElicitationDialog.vue` (`src/components/common/ElicitationDialog.vue`): reusable modal with ⚡ amber-accented header, server message quote block, schema-aware field rendering (boolean toggle, enum select, number, password, URI, string), step counter for multi-step flows, Accept / Decline actions

### Added
- Frontend auto-retry on tool invocation failure: up to 3 attempts with an automatic reconnect between each; Execute button label updates to `Retrying (1/2)…` / `Reconnecting (1/2)…` during the loop
- "Reconnect & Retry" button appears inline in the error panel after all retry attempts are exhausted — no sidebar hunting required
- `MAX_INVOKE_RETRIES` constant (3) in `ToolsView.vue` controls frontend retry budget
- `invokingLabel` and `retryExhausted` refs drive the dynamic button label and contextual retry action
- `retryExhausted` is cleared automatically when the user changes tool or connection


### Added
- Connection health tracking: `IActiveConnection.IsHealthy` flag, set to `false` when all tool-invoke retry attempts are exhausted and restored to `true` on the next successful call
- `GET /connections/active` now includes `isHealthy` in each active connection entry
- Per-connection reconnect semaphore (`_reconnectGates`) in `ConnectionService` — serialises concurrent reconnect attempts so only one caller reconnects while others wait and benefit from the result
- 🔄 reconnect button appears next to degraded connections in the Tools view sidebar; clicking it calls the connect endpoint and reloads tools on success
- Amber dot and amber left-border on unhealthy connection items in the sidebar

### Fixed
- `ReconnectAsync` failure now correctly marks the connection as unhealthy and surfaces the reconnect error; previously the exception escaped the retry loop leaving `IsHealthy` unset
- Frontend `reconnecting` state migrated from `ref<Set<string>>` to `ref<Record<string, boolean>>` for cleaner Vue 3 reactivity (no in-place mutation)
- After a failed tool invocation the connections store is refreshed so the UI immediately reflects the degraded state without requiring a page reload


### Added
- `GET /api/v1/system/info` endpoint returns `apiVersion` (from assembly `AssemblyInformationalVersion`) and `dotnetVersion` (from `RuntimeInformation.FrameworkDescription`)
- Topbar version pills: `.NET runtime`, `UI vX.Y.Z`, `API vX.Y.Z` — all dynamic, not static values
- "Created by Garrard Kitchen" author credit in topbar with email tooltip on hover (`garrardkitchen@gmail.com`)
- `src/frontend/src/api/system.ts` typed API client for the new system info endpoint
- `src/frontend/src/env.d.ts` declaring `__APP_VERSION__` global injected by Vite at build time
- Vite `define.__APP_VERSION__` reads `package.json#version` at build time — no runtime API call needed for frontend version

## [Unreleased] - 2025-04-04

### Fixed
- Prompt invocation blocks now persist across chat reloads — `PromptName` and `PromptInvocationParams` fields added to `ChatMessage` domain model and threaded through `SendMessageRequest`, controller, and `GetMessages` response
- Prompt picker no longer injects a synthetic client-side `system` message; metadata travels with the user message so no second bubble appears
- `isPromptInvocation` now matches `role: 'user'` messages with `promptName` set (not `role: 'system'`)
- `isToolCall` guard simplified (no longer needs to exclude prompt messages)

### Added
- Expand/collapse toggle on prompt invocation blocks to reveal the full rendered prompt content sent to the AI
- `parsePromptParams` helper in ChatView parses the JSON-string `promptInvocationParams` for key/value chip rendering
- `expandedPromptContent` set and `togglePromptContent()` in ChatView for per-message expand state
- CSS classes `.prompt-content-expanded`, `.prompt-content-pre`, `.prompt-content-toggle`, `.prompt-no-params-hint`

All notable changes to MCP Explorer v2 are documented here.

## [Unreleased] — 2026-04-04 (24)
- feat: Prompt invocation block (Design A) — violet card with gradient header, connection badge, model badge, parameter key-value chips, and "↳ sent to chat" hint; distinct from amber tool-call blocks
- feat: `ChatMessage` type extended with `promptName` and `promptInvocationParams` (client-side only)
- fix: Prompt Picker "Run in Chat" now pushes invocation block into timeline before sending prompt result to AI

## [Unreleased] — 2026-04-04 (23)
- feat: Stats dialog redesigned — hero banner with animated token ratio bar, 4 metric cards with icons, tool call breakdown list grouped by tool+connection with call count badges
- feat: Prompt Picker now sends the executed prompt directly into chat (calls `sendMessage`) instead of pasting raw text into the textarea — button renamed "Run in Chat"

## [Unreleased] — 2026-04-04 (22)
- feat: Slash command menu in Chat (`/prompt`, `/recent1-3`, `/stats`, `/report`, `/system`, `/model`, `/clear`) — floating Design A popup with fuzzy search, keyboard nav (↑↓↵ Esc), group headers, icon badges, and enter-hint
- feat: Prompt Picker dialog — browse prompts from all connected servers, filter, select, fill parameters, inject result into chat input
- feat: Token Stats dialog (`/stats`) — shows input/output/total tokens and AI turn count for the session
- feat: Model Picker dialog (`/model`) — quick-switch active model inline
- feat: `/clear` clears current session via confirm dialog
- feat: `clearMessages()` added to chat Pinia store
- fix: Input placeholder updated to hint at `/` commands

- fix: Nav accent colours now use CSS custom properties (`--nav-accent-blue/amber/teal/violet`) per theme instead of hardcoded hex — all 6 themes now render correct accent tints
- fix: Added `aria-label="Main navigation"` to sidebar `<nav>` for accessibility

## [Unreleased] — 2026-04-04 (20)
- feat: Left navigation redesigned with Design A — 4 grouped sections (Infrastructure, Configuration, MCP Explorer, Intelligence) with colour-coded headers and separators
- feat: Rename "Sensitive" → "Data Guard" in sidebar label, router meta, and page title
- feat: Active nav items show per-group accent colour (blue/amber/teal/violet) as left border + background tint
- feat: Group headers hidden when sidebar collapses to icon-only mode

## [Unreleased] — 2026-04-04 (19)
- fix: Tool calls now appear in real-time during streaming — root cause was SSE event name mismatch (`toolcall` vs `tool-call`); backend now uses explicit kebab-case switch for `ToolCall → tool-call`
- fix: Duplicate tool calls caused by LLM receiving user message twice — `session.Messages` snapshot is now taken BEFORE adding the new user message, so `BuildMessages` doesn't append it again
- fix: Tool call messages (role=System) filtered out of LLM history context — they were being sent to the LLM as system prompts, causing confusing re-calls on subsequent turns
- refactor: Chat store simplified — removed in-progress `assistantMsg` placeholder from `messages.value` during streaming; tool calls now push (not splice) and appear above the streaming placeholder naturally; final assistant message is pushed on stream completion

## [Unreleased] — 2026-04-04 (18)
- fix: Token usage (input/output/total) now persists — backend captures `TokenUsage` from stream and saves it to the assistant message in settings.json; was previously never saved
- fix: Tool call messages now appear in real-time as they are invoked — added `await nextTick()` after each splice to flush Vue's DOM update queue immediately
- fix: Vue reactive update for `tokenUsage` now uses object replacement (`{ ...msg, tokenUsage }`) instead of direct property mutation to guarantee reactivity
- feat: `ModelName` now attached to tool call messages and user messages so both can display the model pill
- feat: 🤖 robot avatar moved inside the assistant bubble and wrapped in a circle (`.asst-avatar-circle`)
- feat: All emojis in chat messages placed in CSS circles — user 👤, assistant 🤖, tool call 🔧
- feat: Tool call blocks now show a model pill next to the connection badge
- feat: User message bubbles now show a model pill (indicating which model was selected at send time)
- feat: Token badge redesigned — shows `↑in ↓out total` in green monospace with border; distinct from thinking badge

## [Unreleased] — 2026-04-04 (17)
- fix: Load test results and snapshots now save correctly — background `Task.Run` was capturing the HTTP request's `CancellationToken` which gets cancelled when the response is returned; changed to `CancellationToken.None` for both `RunLoadTestAsync` and `SaveResultAsync`
- fix: `📊` chart button is no longer disabled for new runs (snapshots now persist)
- fix: Progress bar and percentage label no longer show decimal places (`Math.round()`)
- feat: Load test form redesigned as a single compact horizontal row (Connection flex-1, Duration + Max Parallel side-by-side, Run button)
- feat: Load test history item stats now show duration and max parallel as a grouped pair (⏱ Xs · ⚡ Y×) with totals/success/fail right-aligned
- feat: Load test history list expanded from `max-height:220px` to `max-height:340px` for better space utilisation

## [Unreleased] — 2026-04-04 (16)
- feat: Load test history items now show duration, max parallel, total/successful/failed executions inline
- feat: Load test now runs asynchronously — `POST /{id}/load-test` returns `{ runId }` immediately; frontend polls `GET /workflows/load-test-progress/{runId}` every second
- feat: Progress dialog shows live percentage bar, total executions, successful, failed, and active counts during execution
- feat: `📊` button on each history item opens a chart dialog with line graph of Cumulative Successes, Cumulative Failures, and Active Executions (dashed) over elapsed time — powered by Chart.js via PrimeVue Chart component
- feat: `LoadTestResult` gains `DurationSeconds`, `MaxParallelExecutions`, and `Snapshots[]` (one per second) fields
- feat: `LoadTestProgress` domain record tracks live run state; `LoadTestService` stores per-runId progress in `ConcurrentDictionary`
- fix: `active` execution counter tracked via `Interlocked.Increment/Decrement` around each task

## [Unreleased] — 2026-04-04 (15)
- fix: `LoadTestService` now derives its storage directory from `IUserPreferencesStore.StoragePath` instead of the platform-specific path — results are written to `/data/load_tests` inside the container (the mounted volume) rather than the ephemeral `~/.local/share/McpExplorer/load_tests`
- fix: `LoadTestService` is now properly injected with `IUserPreferencesStore` via constructor DI; static initialiser removed

## [Unreleased] — 2026-04-04 (14)
- feat: Load test results are now persisted to disk after each run via `LoadTestService.SaveResultAsync`
- feat: New `GET /api/v1/workflows/{id}/load-test-history` endpoint returns all saved load test runs for a workflow
- feat: Load Test tab now shows a scrollable history list — click any entry to expand its full stats (P50/P90/P99, req/s, connection name, error rate badge)
- feat: `LoadTestResult` gains `WorkflowName` and `ConnectionName` fields for richer history display
- fix: `WorkflowsController.LoadTest` now injects `LoadTestService` and calls `SaveResultAsync` — previously results were discarded after every run

## [Unreleased] — 2026-04-04 (13)
- feat: Workflow execute now detects `PromptAtRuntime` parameter mappings and shows a dialog to collect values before running — values are passed as `runtimeParameters` to the API

## [Unreleased] — 2026-04-04 (12)
- fix: `ImportFromJson` now uses `PropertyNameCaseInsensitive = true` so both PascalCase (exported files) and camelCase (settings.json) workflow JSON are deserialized correctly
- fix: `WorkflowsController.Import` now accepts `[FromBody] JsonElement` — resolves 400 Bad Request when Axios posts a JSON object body
- fix: Frontend `importFromJson` now sends the parsed object directly to Axios instead of re-stringifying, preventing `text/plain` body rejection
- fix: `workflowsApi.importFromJson` parameter type changed from `string` to `unknown` to match the corrected call site

## [Unreleased] — 2026-04-03 (11)
- fix: Parameter field in workflow step mappings is now a `Select` dropdown populated from the selected tool's `inputSchema.properties` — prevents entering invalid parameter names; falls back to `InputText` when tool schema is unavailable
- fix: `connectionToolsMap` now stores full `ActiveTool[]` objects (was just names) so `inputSchema` is available for parameter name extraction
- fix: Extract `McpToolResultHelper.ConvertToJson` shared helper — used by both `WorkflowService` and `ConnectionService.InvokeToolAsync` so all tool result JSON is consistent (real response, not raw MCP content blocks)
- fix: `isError` flag now propagated for all code paths in tool result conversion, including when `StructuredContent` is present or the text is valid JSON (HIGH severity bug caught in code review)
- fix: Property path browser now falls back to live tool invocation of the previous step (using ManualValue params) when no cached execution output exists — no longer requires executing the workflow first
- fix: Don't cache empty tool lists on connection errors — allows re-fetch once the connection becomes available (MEDIUM severity bug caught in code review)
- fix: `ConnectionService.InvokeToolAsync` now returns real tool response JSON instead of raw MCP content blocks — also fixes ToolsView display
- fix: Port `ConvertToolResultToJson` from v1 — `outputJson` now contains real tool response JSON instead of raw MCP `[{type, text}]` content blocks; fixes property path browser, manual-value chaining, and downstream property extraction
- fix: Tool name field in workflow step editor is now a `Select` dropdown populated from the connected connection's tools (falls back to `InputText` when connection is not connected or has no tools)
- fix: `sourceStepIndex` dropdown no longer loses persisted value — replaced `null` option value with `'__auto__'` sentinel to avoid PrimeVue Select's known issue with falsy option values
- fix: Watch `defaultConnectionName` in workflow edit dialog to eagerly load tool list when connection changes

- fix: Port array iteration execution from v1 — `ExecuteStepWithIterationAsync`, `ExtractArrayElementsForIteration`, `NavigateJsonPath`, `ApplyIterationModeFilter` now handle Each/First/Last iteration modes
- fix: `ParseJsonPath` now recognises empty brackets `[]` as array iteration marker (supports `data[].id` paths)
- fix: `BuildParameters` accepts `iterationOverrides` dict and skips mappings with empty `targetParameter`
- fix: Guard negative array indices in `ExtractJsonPathValue` and `NavigateJsonPath`
- fix: Clear error message when first workflow step attempts array iteration (no previous results)
- feat: Property path browser — 🔍 button next to sourcePropertyPath opens a dialog listing all selectable paths extracted from the previous step's output JSON (from live run or most recent history)
- fix: `openEdit` now normalises stepNumbers to 1-based so "previous step" dropdown generates correct option count
- fix: `openPathBrowser` uses array indexing (not stepNumber matching) for reliable result lookup across 0-based and 1-based stored data

## [Unreleased] — 2026-04-03 (8)
- fix: ParameterMapping rewritten to match v1 model — TargetParameter, SourceType (ManualValue/FromPreviousStep/PromptAtRuntime), SourceStepIndex, SourcePropertyPath (JSONPath with array indexing), ManualValue, IterationMode (None/Each/First/Last)
- fix: WorkflowService.BuildParameters ported from v1 — proper MappingSourceType switch, ExtractJsonPathValue with bracket notation, ConvertJsonNodeToValue
- feat: Workflow edit dialog now shows full parameter mapping UI per step — source type selector, conditional fields for each mode, array iteration mode when path contains brackets
- fix: Tests updated to use new ParameterMapping field names

## [Unreleased] — 2026-04-03 (7)
- feat: Workflow execution viewer — rich step-tabs UI showing Input Parameters and Output Result JSON per step
- feat: WorkflowStepResult now persists InputJson/OutputJson/StepExecutionStatus for new executions
- feat: MCP error banner shown when tool response contains isError=true
- feat: Legacy history entries with `result` field still render correctly via fallback

## [Unreleased] — 2026-04-03 (6)

- Add: Connection Groups CRUD — `GET/POST/PUT/DELETE /api/v1/connections/groups` now persists `ConnectionGroup` objects (name, color, description) in `UserPreferences.ConnectionGroups` instead of deriving group names from connections
- Add: ConnectionsView group tabs show colour dots and per-group edit/delete buttons; "Add group" button opens a create dialog with name, colour picker, and description fields
- Add: Per-field parameter history in ToolsView — a clock icon appears beside each parameter label when history exists; clicking it opens an overlay with previous values for that specific field
- Add: Required parameter validation in ToolsView — `invoke()` validates required fields before calling the API and highlights missing fields with a warning toast; errors clear as fields are filled


- Add: System Prompt editor in chat toolbar — "System Prompt" button (disabled until a model is selected) opens a dialog showing the current model's system prompt in an editable textarea; saving via `PATCH /api/v1/llm-models/{name}/system-prompt` persists the change and updates the local model immediately

## [Unreleased] — 2026-04-03 (4)

- Fix: tool call parameters now collapsed by default; removed auto-expand on session load and message arrival
- Add: 📁/📂 toggle button in chat toolbar (next to Markdown switch) to expand/collapse all tool call parameters at once — 📁 means collapsed, 📂 means expanded; button is disabled when no tool calls are present
- Fix: `enc:` prefixed values in tool call parameters are now decrypted server-side via `ISecretProtector` before being returned by `GET /api/v1/chat/sessions/{id}/messages` — values stored by the v1 Blazor app are now readable when revealed

## [Unreleased] — 2026-04-03 (3)

- Fix: backfill `toolCallParameters` and `connectionName` for old migrated chat sessions — v1 `parameters`/`toolName` fields were not mapped during initial migration; a second one-time migration pass (`BackfillV1ToolCallParameters`) reads v1 session files and fills in the missing data, preserving any sessions created after v2 launched
- Fix: add `Parameters` field to `V1ChatMessage` DTO so future re-migrations also capture it

## [Unreleased] — 2026-04-03 (2)

### Fixed
- **Model dropdown auto-select**: Selecting an existing chat session now pre-fills the model dropdown with the model used in that session's last assistant message, so continuing a conversation uses the same model by default
- **Tool call params not expanding**: `expandedParams` Set used temporary `crypto.randomUUID()` IDs during streaming; after `loadSessions()` reloaded messages with server-assigned GUIDs the IDs no longer matched and params stayed collapsed. Fixed by (1) `selectSession()` eagerly adds all tool call message IDs to `expandedParams` after load, and (2) the `messages.length` watcher adds IDs for any newly arrived tool call messages during streaming
- **Tool call params auto-expanded**: Parameters are now expanded by default when a session is loaded or a tool call arrives — no extra click needed

### Added
- **Sensitive parameter masking**: Tool call parameters with property names matching sensitive patterns (`key`, `password`, `token`, `secret`, `auth`, `credential`, `apiKey`, `clientSecret`, `accessToken`, etc.) are automatically masked with `████████` in the params display
- **Sensitive reveal toggle**: A `🔒 Contains sensitive values · 👁 Reveal values` banner appears below masked params; clicking it shows/hides the actual values (scoped per message)
- **Connection pill styling**: The `🔌 <connection>` pill next to tool names is now teal-coloured to visually distinguish it from the yellow tool call border

## [Unreleased] — 2026-04-03

### Added
- **Chat markdown rendering**: Assistant messages now render full GitHub-flavored markdown (tables, code blocks, headings, lists) using `marked` + `DOMPurify`; raw text fallback when disabled
- **Markdown toggle**: ToggleSwitch in chat toolbar (on by default) to switch between rendered and raw text
- **Role avatars**: 🤖 robot emoji for assistant messages, 👤 person emoji for user messages — both appear in the Report transcript
- **Tool call display**: Tool invocations during streaming appear as distinct yellow-bordered blocks (🔧) showing connection badge, tool name, timestamp, and collapsible JSON parameters
- **Token usage badges**: 💰 `in X · out Y` badges on assistant messages when token counts are available
- **Thinking time badges**: 🤔 `[mm:ss]` badges on assistant messages showing time-to-first-token
- **Copy message button**: Per-assistant-message clipboard copy button
- **Chat Session Report dialog**: "Report" button in toolbar opens a PrimeVue Dialog with Raw Markdown and Preview tabs; generates Executive Summary (topic, message count, duration, models used) and full timestamped transcript; Copy and Download (`.md`) actions
- **Sessions sorted newest-first**: Both backend (`GetSessions` endpoint) and frontend store sort sessions by `lastActivityUtc` descending
- **Auto-named sessions**: New sessions are named `Chat YYYY-MM-DD HH:mm` rather than generic "New Chat"
- **Delete confirmation**: Deleting a chat session (or any item) shows a confirmation dialog before proceeding — includes item name and "This cannot be undone" warning
- **`ChatMessage` extended**: Domain model now carries `ConnectionName` (which MCP server the tool call came from) and `ThinkingMilliseconds` (time-to-first-token)
- **V1 migration extended**: Migrated messages now map `ConnectionName` and `ThinkingMilliseconds` from v1 data

### Fixed
- **`ChatReportDialog` TabPanel**: Updated to PrimeVue 4 `Tabs/Tab/TabList/TabPanels/TabPanel` API (removed deprecated `TabView` usage that caused TypeScript build error)

## [Unreleased] — 2026-07-15

### Fixed
- **Chat session migration**: v1 session files whose messages have `null` token fields (`inputTokens`, `outputTokens`, `totalTokens`) now import correctly — the `V1ChatMessage` DTO fields are now `int?` instead of `int`, preventing `System.Text.Json` from throwing during array deserialization and silently dropping all messages. All 24 imported sessions now show correct message counts
- **Chat session migration — not persisted**: Migrated sessions were held in memory but never written to `settings.json`. On container restart the `.v1-migration-complete` marker existed so migration was skipped, returning 0 sessions. Fixed by extracting `WriteUnlockedAsync` (lock-safe write helper) from `SaveAsync` and calling it from within the `LoadAsync` critical section immediately after migration, so sessions survive restarts. The stale marker file is also deleted automatically on next start
- **Pages require refresh**: Added `initialized` flag to the connections store; App.vue sets it after completing both `loadSaved()` and `loadActive()` in parallel. Views (Tools, Prompts, Resources, Resource Templates, Chat) use a single `watch({ immediate: true })` on this flag — handles both the "view mounts after init" and "view mounts before init" race conditions without duplicate API calls
- **`loadActive()` concurrency guard**: The connections store now uses a `loadingActive` flag to prevent concurrent calls from multiple simultaneous watchers or navigations


### Added
- **v1 chat session migration**: On first load, sessions stored by the original Blazor app as individual files in `ChatSessions/` are automatically imported into v2 format — no manual steps required
- **PATCH /api/v1/chat/sessions/{id}**: New endpoint to rename chat sessions; wired up to the existing frontend rename UI
- **JSON viewer search**: Full text search with mark.js highlights all matches; prev/next navigation buttons (also bound to Shift+Enter/Enter); active match shown in a distinct amber colour and auto-scrolled into view; match counter (`N / M`) in toolbar; `No matches` indicator in red; Escape clears search; search is shared between inline and fullscreen views

### Fixed
- **Tools count badge**: Now shows `filtered / total` (e.g. `3 / 12`) with amber badge when a search filter is active, and plain total when no filter is applied
- **Markdown anchor links**: In-page `#section` links in the documentation preview now scroll within the dialog instead of navigating the browser URL
- **MCP tool/prompt/resource icons**: Tools, Prompts, Resources, and Resource Templates now display server-provided icons when available. Falls back to a coloured initial badge when no icon is supplied. Icons are extracted from the MCP SDK's protocol layer via reflection (`ProtocolTool.Icons`, `ProtocolPrompt.Icons`, etc.) using the new `McpIconHelper` in the Infrastructure layer
- **`McpIconHelper`**: New static helper that extracts the best icon URL from an MCP protocol object — prefers theme-matched icons, falls back to first available `Source`. Exception-safe
- **JSON Viewer**: Replace hand-rolled syntax highlighter with `vue-json-pretty` v2.6.0 for collapsible tree rendering, node hover highlighting, and built-in search filtering
- **Tool Documentation Dialog**: New `ToolDocsDialog` component renders MCP tool docs (name, description, parameter table, input schema) as markdown with Preview and Raw Markdown tabs; accessible via a book icon button in the tool detail header
- **`useToolDocs` composable**: Generates structured markdown from `ActiveTool` using `marked` v17; exports `generateToolMarkdown` and `renderMarkdown` helpers


### Added
- **JSON Viewer**: Syntax highlighting for keys, strings, numbers, booleans, and null values using CSS token classes (`json-key`, `json-str`, `json-num`, `json-bool`, `json-null`)
- **Preferences PATCH endpoint**: `PATCH /api/v1/preferences` for partial updates to favorites, parameter history, and other preference fields
- **`ParameterHistory`** property on `UserPreferences` — persists per-tool invocation history (up to 5 entries each) to the backend
- **Tools view**: Favourites-first toggle button in the tool list header panel; tool list renders a section header divider when enabled
- **Tools view**: History items now rendered as `Button` components with tooltip preview instead of bare divs

### Changed
- **Tools view**: Favourites now persisted to backend via `PATCH /api/v1/preferences` instead of `localStorage`
- **Tools view**: Parameter history now persisted to backend instead of `localStorage`
- **Chat sessions**: `StreamMessage` now saves both the user message and the accumulated assistant response to the session before closing the SSE stream

### Fixed
- **Chat sessions**: Messages were not persisted after streaming — `prefs.ChatSessions` is mutated in-place and `SaveAsync` is called after streaming completes
- **Chat view**: `deleteSession` now shows a PrimeVue `ConfirmDialog` before deleting (was instant)

## [Unreleased] — 2026-04-03 (patch 3)

### Fixed
- Connections: Custom header values (e.g. `X-API-KEY`) now correctly decrypted on load — `PreferencesMapper` was gating decrypt/encrypt on `IsAuthorization`, which is `false` for all non-`Authorization` headers. V1 encrypts/decrypts ALL header values unconditionally; v2 now matches this behaviour. `SecretProtector.Decrypt()` is a no-op for values without the `enc:` prefix, so this is safe for both v1 and v2 connections.

## [Unreleased] — 2026-04-03 (patch 2)

### Fixed
- Connections: Azure/OAuth connect was sending the raw `enc:…` ciphertext as credentials instead of the decrypted secret — root cause was `SecretProtector` generating a fresh random key inside the container because it looked at `~/.local/share/McpExplorer/secret.key` (empty) instead of the mounted `/data/secret.key`
- `SecretProtector` refactored from static key singleton to instance-based with optional `keyDirectory` constructor parameter
- `ServiceCollectionExtensions` now derives `keyDirectory` from `PREFERENCES__StoragePath` path so `secret.key` is loaded from the same mounted volume as `settings.json`


### Fixed
- Connections: `authenticationMode` now serializes as string (e.g. `"CustomHeaders"`) not integer `0` — `JsonStringEnumConverter(allowIntegerValues:true)` added to both API and persistence
- Connections: v1 settings.json field names `azureClientCredentials`, `oAuthCredentials`, `selectedConnection` are normalized on load, fixing null credentials for Azure/OAuth connections
- Connections: 500 on connect no longer occurs when `AzureCredentials` was null due to v1 field name mismatch
- Connections: `PUT /api/v1/connections/{name}` update endpoint added (was missing, causing silent no-op on edit)
- Connections: `GET /api/v1/connections/groups` endpoint added — returns distinct group names from saved connections
- Connections: `CreateConnectionRequest` DTO now includes `AuthenticationMode`, `Headers`, `AzureCredentials`, `OAuthOptions`
- Connections: Controller `POST /` and `PUT /{name}` map all fields via shared `MapFromRequest` helper
- Connections: Edit dialog now pre-initializes `azureCredentials`/`oAuthOptions` objects so v-model bindings never crash on null
- Connections: Switching auth mode in dialog initializes the relevant credential object immediately
- Connections: Auth type badge in table shows human-readable label (`"Custom Headers"`) and severity colour, handles integer fallback
- Connections: `NormalizeV1Json` upgraded from string-replacement to `JsonNode`-based structural rename, eliminating false matches on v1 field name strings inside note/value fields
- Connections: `DELETE /{name}` and `POST /{name}/connect` now use case-insensitive name comparison (was case-sensitive `==`)
- Connections: `PUT /{name}` now checks for name collision before allowing a rename
- Connections: Frontend save() validates required credential fields before submit (Azure: tenant/client/secret/scope; OAuth: clientId/redirectUri/scopes)
- Connections: `AUTH_MODE_SEVERITY` map includes integer fallbacks (`'0'`, `'1'`, `'2'`) matching `AUTH_MODE_LABELS`



### Changed
- Replaced YARP gateway (`Dockerfile.gateway`) with `nginx:alpine` for docker-compose — smaller image (~50 MB vs ~220 MB), better separation of concerns
- Created `docker/nginx.conf`: SPA static file serving, `/api/` + `/oauth/` proxy to API, SSE buffering disabled, immutable asset caching for `/assets/`
- Created `Dockerfile.frontend`: Node build stage → nginx:alpine final stage
- Updated `docker-compose.yml`: `gateway` service replaced with `frontend`
- Added `.gitignore` and `.env` (copied from `.env.example`)
- Added `/healthz` endpoint to API for docker-compose healthcheck
- Fixed healthcheck: `curl` added to `Dockerfile.api`, endpoint changed to `/healthz`, `start_period` raised to 30s

## [Unreleased] — 2025-07-11

### Added
- **Unit tests** (131 tests across 3 projects): `Garrard.Tests.Mcp.Explorer.Core`, `Garrard.Tests.Mcp.Explorer.Infrastructure`, `Garrard.Tests.Mcp.Explorer.Api`
  - Core domain tests: `ConnectionDefinition`, `ChatSession`/`ChatMessage`/`ChatTokenUsage`/`ChatStreamEvent`, `WorkflowDefinition`/`WorkflowExecution`, `UserPreferences`/`SensitiveFieldConfiguration`
  - Infrastructure tests: `SecretProtector` (encrypt/decrypt round-trips, random IV, corrupt-data resilience), `UserPreferencesStore` (save/load, encryption delegation, concurrent writes), `ConnectionHeaderBuilder` (Bearer/Basic/custom scheme prefix logic)
  - API tests: `PreferencesController` and `ConnectionsController` using Moq mocks (no `WebApplicationFactory`)
- `ConnectionHeaderBuilder` — extracted `MaterializeHeaders` from `ConnectionService` into a dedicated public static class for testability; `ConnectionService.MaterializeHeaders` now delegates to it
- `[Required, MinLength(1)]` validation attribute added to `UpdateThemeRequest.Theme` so the ASP.NET Core pipeline returns 400 for empty theme strings

- Complete rewrite from Blazor Server to Vue 3 / Vite / PrimeVue frontend + ASP.NET Core Web API backend
- Clean Architecture: `Core` (domain + interfaces) → `Infrastructure` (MCP SDK, LLM, persistence) → `Api` (controllers) → `Gateway` (YARP)
- 6 selectable UI themes: Command Dark, Command Light, Nord, Dracula, Catppuccin Mocha, Solarized Light — persisted server-side and cached in `localStorage` for instant load
- Command palette (Ctrl+K / ⌘K) for keyboard-driven navigation
- SSE streaming for chat responses (POST body via `fetch` + `ReadableStream`, not `EventSource`)
- SSE streaming for live elicitation requests using `Channel<T>` pattern
- YARP reverse proxy gateway (`Garrard.Mcp.Explorer.Gateway`) routing `/api/*` → API, Vue SPA fallback for all other routes
- Single-container deployment via `Dockerfile` + `supervisord` (Gateway on `:8080`, API on `:5000`)
- Multi-container deployment via `docker-compose.yml` with `api` + `gateway` services and YARP cluster override via env var
- `.env.example` documenting all environment variables
- 10 fully-implemented Vue views: Connections, Tools, Prompts, Resources, Resource Templates, Chat, Workflows, AI Models, Sensitive Fields, Elicitations
- Dynamic parameter forms for tools and prompts based on JSON Schema / argument definitions
- Workflow step builder with add/remove/reorder, execution history, and load test stats
- Elicitation respond dialog with schema-aware fields
- `JsonViewer` component with search highlighting and fullscreen mode
- `ThemeSwitcher` overlay panel component
- Typed Pinia stores: connections, chat, preferences, theme, workflows, notifications
- Full typed TypeScript API client layer mirroring all Core domain models
- 129 unit tests across Core (66), Infrastructure (36), and API (27) projects
- `NuGet.Config` isolating solution to `nuget.org` feed (avoids corporate auth failures)

### Architecture
- `Garrard.Mcp.Explorer.Core` — domain models + interfaces, zero framework dependencies
- `Garrard.Mcp.Explorer.Infrastructure` — MCP SDK, LLM providers (OpenAI/Azure), UserPreferencesStore (AES-256), SecretProtector, ConnectionService, AiChatService, WorkflowService, ElicitationService
- `Garrard.Mcp.Explorer.Api` — 9 versioned controllers (`api/v1/`), global exception middleware, correlation ID middleware, Swagger
- `Garrard.Mcp.Explorer.Gateway` — YARP proxy, static files, SPA fallback
- `Garrard.Mcp.MessageContentProtection` — sensitive data detection (regex, heuristic, AI-enhanced)
