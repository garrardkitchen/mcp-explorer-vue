---
title: "Building Workflows"
description: "Create and run multi-step MCP tool chain workflows."
weight: 1
---

## Overview

Workflows let you chain MCP tool calls into repeatable, parameterised sequences. Define the steps once, run them on demand, track history, and even load-test your server with concurrent executions.

<img src="/images/screenshots/36-workflows-list.png" alt="Workflows list — empty state with New Workflow button" style="max-width:100%;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.15);cursor:zoom-in">

*The Workflows list. Click **New Workflow** to create your first workflow, or **Import** to load one from a JSON file.*

---

## Creating a Workflow — Step-by-Step Example

The walkthrough below creates an **Echo and Lookup** workflow that:
1. Prompts for a name at runtime using the `aaa_echo` tool
2. Passes the echoed value into the `who_is` tool to look up that person

### Step 1 — Name your workflow

Click **New Workflow**, enter a name, then click **Add Step**.

<img src="/images/screenshots/37-workflows-new.png" alt="New Workflow dialog with name filled in" style="max-width:100%;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.15);cursor:zoom-in">

*Give the workflow a clear name. You can also set a default connection to avoid selecting it every time you run.*

### Step 2 — Configure Step 1 (aaa_echo)

Select the connection (`mcp server`) and enter `aaa_echo` as the tool name.

<img src="/images/screenshots/39-workflow-step1-tool.png" alt="Step 1 configured with mcp server and aaa_echo tool" style="max-width:100%;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.15);cursor:zoom-in">

*Step 1 uses the `mcp server` connection and the `aaa_echo` tool.*

### Step 3 — Add a Prompt at Runtime mapping

Click **Add Mapping**, set the **Parameter** to `echo`, and change the **Source** to **Prompt at Runtime**. This tells the workflow to ask for the value each time it executes.

<img src="/images/screenshots/40-workflow-step1-prompt-at-runtime.png" alt="Parameter mapping with Prompt at Runtime selected for echo" style="max-width:100%;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.15);cursor:zoom-in">

*The `echo` parameter is set to **Prompt at Runtime** — the workflow will pop up a dialog asking for this value before executing.*

### Step 4 — Configure Step 2 (who_is)

Click **Add Step**, select `mcp server` and enter `who_is` as the tool. Then click **Add Mapping** and set `fullname` as the parameter with source **From Previous Step**.

<img src="/images/screenshots/41-workflow-step2-tool.png" alt="Step 2 configured with who_is tool" style="max-width:100%;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.15);cursor:zoom-in">

*Step 2 uses the `who_is` tool to look up information.*

<img src="/images/screenshots/42-workflow-step2-mapping.png" alt="Step 2 fullname parameter mapped From Previous Step" style="max-width:100%;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.15);cursor:zoom-in">

*The `fullname` parameter is mapped **From Previous Step** — it will automatically receive the output of `aaa_echo` as its input.*

### Step 5 — Save and view the workflow

Click **Create**. The workflow appears in the left sidebar showing its step count.

<img src="/images/screenshots/43-workflow-saved.png" alt="Saved Echo and Lookup workflow in the detail view" style="max-width:100%;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.15);cursor:zoom-in">

*The saved workflow with both steps listed. Click **Execute** to run it.*

---

## Running a Workflow

Click **Execute**. If any step has a **Prompt at Runtime** parameter, a dialog appears asking for those values before execution starts.

<img src="/images/screenshots/44-workflow-runtime-dialog.png" alt="Runtime Parameters dialog prompting for the echo value" style="max-width:100%;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.15);cursor:zoom-in">

*The **Runtime Parameters** dialog collects any values marked as "Prompt at Runtime" before the workflow begins.*

<img src="/images/screenshots/45-workflow-runtime-filled.png" alt="Runtime parameter filled with Garrard Kitchen" style="max-width:100%;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.15);cursor:zoom-in">

*Enter the runtime value — here "Garrard Kitchen" — then click **Run Workflow**.*

The execution panel shows real-time step progress and results:

<img src="/images/screenshots/46-workflow-execution-result.png" alt="Workflow execution results showing both steps completed" style="max-width:100%;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.15);cursor:zoom-in">

*Both steps completed ✓ in 107ms. Step 1 echoed "Garrard Kitchen" and Step 2 received that output as the `fullname` input — showing the full data pipeline in action.*

---

## Parameter Mapping Sources

Each step mapping can draw its value from one of three sources:

| Source | Description |
|--------|-------------|
| **Manual Value** | A fixed static value set at design time |
| **From Previous Step** | The output of an earlier step (auto-selects the immediately preceding step) |
| **Prompt at Runtime** | User is prompted for this value each time the workflow is executed |

---

## Running a Workflow

Click **Execute** on the workflow detail view. For workflows with **Prompt at Runtime** parameters, a dialog collects the values before execution begins. The panel then shows real-time step progress, input/output per step, and total duration.

---

## Execution History

Every workflow run is saved automatically. Click **History** to view past runs including timestamps, duration, and per-step status.

<img src="/images/screenshots/47-workflow-history.png" alt="Workflow history showing a completed run" style="max-width:100%;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.15);cursor:zoom-in">

*The history panel lists every execution. Click any run to expand the full step-by-step input/output detail.*

You can **delete** individual runs from the history with a confirmation dialog.

---

## Import & Export

Workflows can be exported to a JSON file and imported — useful for sharing workflow templates with a team or backing up your work.

- **Export**: Click the **Export** button on the workflows list
- **Import**: Click **Import** and select a `.json` file

---

## Load Testing

For each workflow you can run a **load test** — execute the workflow N times concurrently to stress-test your MCP server:

1. Open a workflow
2. Click **Load Test**
3. Set the number of concurrent runs and total iterations
4. Click **Start** — results show success rate, average duration, and errors

---

## Workflow Tips

- Use workflows to automate repetitive MCP exploration tasks
- Combine a search tool with a summarisation prompt for quick research pipelines
- Use load testing to find throughput limits before deploying to production
