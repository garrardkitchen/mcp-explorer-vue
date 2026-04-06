# MCP Explorer — Feature Reference

Features grouped by navigation section and menu item.

---

## 🌐 Global / App Shell

- **Collapsible sidebar** — toggle the navigation panel to reclaim screen space; nav items show icon-only tooltips when collapsed
- **Navigation groups** — menu items are grouped into colour-coded sections (Infrastructure, Configuration, MCP Explorer, Testing)
- **Command palette** (`⌘K` / `Ctrl+K`) — fuzzy-search and jump to any page instantly from the keyboard
- **Theme switcher** — choose from 10 built-in themes; selection is persisted per user
  - Command Dark, Command Light
  - Nord, Dracula, Catppuccin Mocha
  - Solarized Light
  - GitHub Dark, GitHub Light
  - Material Dark, Material Light
- **Version pills** — topbar shows live .NET runtime version, frontend version, and API version
- **Active connections badge** — topbar indicator showing how many MCP connections are currently live

---

## 🔌 Infrastructure › Connections

### Connection Management
- **Create** a new connection with name, endpoint URL, optional description, and group assignment
- **Edit** an existing connection inline (all fields including name)
- **Delete** a connection with confirmation dialog
- **Rename** a connection (name change updates all references)
- **Group assignment** — assign each connection to a named group for organisation
- **Connection list** — sorted and filterable; supports search by name/endpoint

### Connection Groups
- **Create / edit / delete groups** — each group has a name, colour, and optional description
- **Rename a group** — cascades the rename to all member connections automatically
- **Group filter** — click a group chip to filter the connections list

### Connection Display Options
- **Show/hide timestamps** — toggle created/updated timestamps in the connection list
- **Sort order** — change the order connections are listed
- **Group view toggle** — switch between grouped and flat connection views

### Health & Connectivity
- **Connection health badge** — live status indicator (healthy / degraded / unknown)
- **Manual reconnect** — button to reconnect a degraded connection without leaving the page

### Favourites
- **Star individual connections** as favourites
- **Show favourites first** toggle — persisted preference

### Export
- **Select connections to export** — multi-select dialog with text filter to find specific connections
- **Select / deselect all** checkbox for bulk selection
- **Password protection** — enter a password before exporting; AES-256-GCM encryption with PBKDF2-SHA256 key derivation
- **Auto-generate password** — cryptographically random 20-character password (upper, lower, digits, symbols)
- **Copy password to clipboard** — with 2-second "Copied!" feedback
- **Download** as `connections-export.json`

### Import
- **Upload exported file** — drag-and-drop or file picker
- **Password prompt** — enter the export password; clear error if the password doesn't match
- **Collision handling** — if an imported connection name already exists, it is automatically renamed `(v2)`, `(v3)`, etc.

---

## ⚙️ Configuration › AI Models

- **Create / edit / delete** LLM model definitions with confirmation on delete
- **Rename** model (name change with collision detection — returns 409 if new name already exists)
- **Provider types supported**: OpenAI, Azure OpenAI, Azure AI Foundry, Ollama, Anthropic, Custom
- **Deployment name field** shown for Azure OpenAI and Azure AI Foundry providers
- **Endpoint URL** field for providers that support a custom base URL
- **API key field** with show/hide toggle (PrimeVue Password component)
- **Masked API key display** in the model list (shows only last 4 characters)
- **Set as default** — mark one model as the default for Chat and Prompts
- **Default indicator** badge in the model list

---

## 🛡️ Configuration › Data Guard

- **Additional sensitive field keys** — define extra JSON property names (beyond the built-ins) that should be masked in the Chat view
- **Custom regex patterns** — add free-text regular expressions to redact arbitrary patterns from chat messages
- **Allowed fields** — whitelist specific field names to exempt them from masking even if they match a sensitive pattern
- **Add / remove** individual entries from each list
- **Save** configuration to the backend (persisted in `settings.json`)
- **Live effect** — chat masking picks up the saved config on next page load

---

## 🔧 MCP Explorer › Tools

### Connection & Tool Selection
- **Connection selector** — pick an active MCP connection from a dropdown
- **Tool list** — alphabetically sorted list of all tools exposed by the selected connection
- **Search / filter** — live text filter to narrow the tool list
- **Tool badge count** — shows how many tools match the current filter vs total
- **Warning badge** — amber badge when the filter is active and results are reduced

### Favourites
- **Star individual tools** — toggle favourite per tool; persisted to the backend
- **Show favourites first** toggle — floats starred tools to the top; persisted

### Tool Invocation
- **Dynamic parameter form** — auto-generated input fields from the tool's JSON Schema
- **Required field validation** — lists missing required fields before invoking
- **Invoke** button with loading state and label feedback ("Invoking…")
- **Retry on exhaustion** — "Retry" banner appears if all retry attempts are spent
- **Server-side elicitation** — if the MCP server sends an elicitation request mid-invocation, a modal dialog appears for the user to respond

### Parameter History
- **Per-tool history** — up to 5 recent parameter sets are stored per tool
- **Load a previous run** — click a history entry to populate the form with past values
- **Delete a run** — remove a history entry with a confirmation dialog (hover to reveal trash icon)
- **Per-field history popover** — click the history icon next to any field to see and re-select previously entered values for that field specifically

### Documentation
- **Single tool docs** — markdown-rendered documentation for the selected tool, including input schema and output schema
- **Bulk tool docs** — view documentation for all visible (filtered) tools in one dialog

### Connection Health
- **Health indicator** per connection chip
- **Reconnect button** per connection chip for quick re-establishment

---

## 📝 MCP Explorer › Prompts

### Prompt Discovery
- **Connection selector** — pick from active MCP connections
- **Prompt list** — alphabetically sorted; live text search/filter

### Favourites
- **Star individual prompts** as favourites; persisted
- **Show favourites first** toggle; persisted

### Prompt Execution
- **Dynamic argument form** — input fields generated from the prompt's argument schema
- **Execute** — submit arguments and display the rendered prompt messages
- **Inline LLM response** — send the executed prompt directly to a configured LLM model without leaving the page
- **Model picker** — popover to select the target LLM model (loads configured models)
- **Streaming response** — LLM reply streams token-by-token into the inline response panel
- **Cancel streaming** — abort button while the LLM is responding
- **Result tabs** — view the Prompt response and the LLM response in separate tabs

### Documentation
- **Single prompt docs** — markdown documentation for the selected prompt
- **Bulk prompt docs** — documentation for all visible prompts in one dialog

---

## 🗄️ MCP Explorer › Resources

### Resource Discovery
- **Connection selector** — pick from active MCP connections
- **Resource list** — alphabetically sorted; live text search/filter

### Favourites
- **Star individual resources** as favourites; persisted
- **Show favourites first** toggle; persisted

### Resource Reading
- **Read resource** — fetch and display the resource content
- **Content rendering** — text/plain, text/markdown (rendered), `application/json` (pretty-printed)

### Documentation
- **Single resource docs** — markdown documentation for the selected resource
- **Bulk resource docs** — documentation for all visible resources

---

## 📋 MCP Explorer › Templates (Resource Templates)

### Template Discovery
- **Connection selector** — pick from active MCP connections
- **Template list** — alphabetically sorted; live text search/filter

### Favourites
- **Star individual templates** as favourites; persisted
- **Show favourites first** toggle; persisted

### Template Reading
- **Dynamic URI parameter form** — input fields derived from the template's URI pattern
- **Read template** — substitute values into the URI template and fetch the resource
- **Content rendering** — same as Resources (plain, markdown, JSON)

### Documentation
- **Single template docs** — markdown documentation for the selected template
- **Bulk template docs** — documentation for all visible templates

---

## 🔔 MCP Explorer › Elicitations

- **Pending elicitation list** — shows all server-initiated elicitation requests awaiting a response
- **Elicitation details dialog** — view the full request content with its schema
- **Respond / Decline** — submit a response or decline the elicitation from the history list
- **Live elicitation dialog** — when a tool invocation triggers an elicitation, a modal appears inline on the Tools or Chat page for immediate interaction
- **Dynamic form rendering**:
  - String, number, boolean fields
  - Enum with ≤ 3 options → radio/checkbox card layout
  - Enum with > 3 options → `Select` (single) or `MultiSelect` (multi) dropdown
- **Elicitation history** — past elicitations are shown with their status (pending / responded / declined)

---

## 🔀 Testing › Workflows

### Workflow Management
- **Create / edit / delete** workflow definitions; delete requires confirmation
- **Multi-step pipeline** — add, remove, and reorder steps with up/down arrows
- **Step renumbering** — automatic when steps are added, removed, or moved

### Workflow Steps
- **Tool selector** — per-step dropdown of tools from the workflow's default connection
- **Parameter mapping** — map input parameters from:
  - A previous step's output (with JSON path selector)
  - A manually entered value
  - A runtime prompt (user is asked at execution time)
- **JSON path browser** — dialog to browse the output schema of a previous step and select a path
- **Array iteration mode** — per-step setting: None, Each (iterate), First, Last
- **Error handling mode** — Stop on Error or Continue on Error

### Execution
- **Execute workflow** — run the full pipeline against a selected connection
- **Runtime parameter prompts** — if any step has prompt-at-runtime mappings, a dialog collects them before execution starts
- **Execution results** — per-step output displayed in result tabs
- **Execution history** — recent runs stored and accessible per workflow

### Load Testing
- **Configure load test** — set duration (seconds) and parallelism (concurrent workers)
- **Run load test** — execute the workflow repeatedly under load
- **Live progress dialog** — shows elapsed time, request count, and in-flight workers during the test
- **Results chart** — bar/line chart of throughput and latency per time bucket
- **Load test history** — previous load test runs stored per workflow

### Import / Export
- **Export workflow** — download a workflow definition as a JSON file
- **Import workflow** — upload a JSON file; name collisions are resolved automatically by appending `(v2)`, `(v3)`, etc.

---

## 💬 Testing › Chat

### Sessions
- **Multiple chat sessions** — create and switch between named conversation sessions in the sidebar
- **Rename session** — double-click or use the rename dialog
- **Delete session** — remove a session with confirmation
- **Session sidebar** — keyboard-navigable list of sessions (`↑`/`↓` arrows)

### Messaging
- **Send messages** — `Enter` to send, `Shift+Enter` for a new line
- **Select LLM model** — pick from configured AI models per session
- **System prompt** — set and edit a custom system prompt per session
- **Streaming responses** — assistant replies stream token-by-token

### Tool Calling
- **Automatic tool call display** — tool calls made by the LLM are shown as collapsible blocks
- **Tool call parameters** — expand to see the JSON parameters sent; pretty-printed
- **Tool call results** — expand to see the raw result returned
- **Sensitive parameter masking** — tool call parameters matching Data Guard rules are automatically redacted with `████`
- **Reveal masked values** — 👁 toggle to show the original values

### Prompt Invocation
- **Prompt picker** — search and select a prompt from any active connection via a dedicated dialog
- **Prompt parameter form** — fill in prompt arguments before injecting
- **Prompt invocation block** — chat messages show the prompt name, connection, and parameter summary; expandable

### Slash Commands
- **`/` trigger** — type `/` in the message box to open the slash command menu
- **Fuzzy search** — filter commands by typing after `/`
- **Keyboard navigation** — `↑`/`↓` to move, `Enter` or `Tab` to select, `Escape` to dismiss
- **Built-in commands**: `/clear` (clear chat history), `/model` (change model), `/system` (set system prompt), `/stats` (show token usage stats)

### Sensitive Data Masking (User Messages)
- **Auto-detect sensitive content** — user messages are scanned against Data Guard patterns before display
- **`🔒 Contains sensitive values` banner** — shown on user message bubbles that were redacted
- **👁 Reveal toggle** — show the original unredacted text per message

### Statistics & Info
- **Token usage stats dialog** — shows prompt tokens, completion tokens, total tokens, and thinking time across the session
- **Per-message metadata** — timestamp, model name, and response time shown for each assistant message
- **Thinking time display** — shows how long the model spent "thinking" (for models that support it)

### Report
- **Export chat report** — generate a formatted report of the conversation (ChatReportDialog)

### Markdown Rendering
- **Markdown-rendered responses** — assistant messages are rendered with full markdown (headings, code blocks, lists, tables, bold/italic)
- **Toggle raw/rendered** — switch between rendered markdown and raw text per session
- **Copy message** — copy the raw text of any message to the clipboard

---

*This document is auto-maintained. Update when adding new features.*
