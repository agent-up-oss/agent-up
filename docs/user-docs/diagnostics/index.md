---
title: Diagnostics
---

<DocEyebrow slice="Diagnostics" status="preview" />

# Diagnostics

<DocFocus>
Console output is the first diagnostic source after a failed start, health check, or browser timeout.
</DocFocus>

## What it is

The Server continuously collects console output, JavaScript exceptions, failed network requests, performance timings, health information, and process status for each workspace. Product crash reporting for Agent-Up itself is separate.

<DocSpine>
<DocBeat selected>Reproduce the failure in the running workspace</DocBeat>
<DocBeat>Read the application console</DocBeat>
<DocBeat>Inspect health, logs, and audit history</DocBeat>
</DocSpine>

<DocContract>agent-up diagnostics</DocContract>

<DocSurface cli>`agent-up diagnostics` shows process and health state, recent application logs, and relevant JavaScript, network, and browser errors. Entries are marked `active` or `resolved`.</DocSurface>

<DocSurface mcp>Call Orchestration MCP `get_workspace_console` first after browser failures. If that tool is unavailable, query Audit MCP for recent `application` events from `process`.</DocSurface>

Next in this slice: [Verification](/docs/verification) for required checks before enqueue.
