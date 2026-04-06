---
title: "Building Workflows"
description: "Create and run multi-step MCP tool chain workflows."
weight: 1
---

## Overview

Workflows let you chain MCP tool calls into repeatable, parameterised sequences. Define the steps once, run them on demand, track history, and even load-test your server with concurrent executions.

![Workflow builder with step list, parameter mapping, and execution controls](/images/screenshots/workflow-builder.png)
*The Workflow builder shows the step list on the left and the execution panel on the right. Each step maps to an MCP tool with its own parameter set.*

---

## Creating a Workflow

1. Click **New Workflow**
2. Give it a name and optional description
3. Add steps — each step is an MCP tool call:
   - Select the **Connection** and **Tool**
   - Fill in static parameters, or reference outputs from previous steps
4. Click **Save**

---

## Step Output References

You can use the output of one step as the input to the next using `{{step.N.output}}` syntax in parameter values. This enables data pipelines — e.g., search → summarise → save.

---

## Running a Workflow

Click **Run** to execute all steps in sequence. The execution panel shows:
- Real-time step progress (pending → running → completed / failed)
- Input and output for each step
- Total duration

---

## Execution History

Every workflow run is saved to history. Click **History** to view past runs with:
- Timestamp
- Pass / fail status per step
- Full input/output for each step
- Total duration

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
