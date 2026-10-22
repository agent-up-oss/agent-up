---
title: Diagnostics
---

<DocEyebrow slice="Diagnostics" status="preview" />

# Diagnostics

<DocWhat>
The Server continuously collects console output, JavaScript exceptions, failed network requests, performance timings, health information, and process status for each workspace.

Product crash reporting for Agent-Up itself is separate.
</DocWhat>

<DocCallout>
Console output is the first diagnostic source after a failed start, health check, or browser timeout. The Server keeps those lines intact: one source line per entry, stdout and stderr identity on the entry, and an explicit `[truncated]` marker when older output is dropped.
</DocCallout>

<DocSpine>
<DocBeat>Reproduce the failure in the running workspace</DocBeat>
<DocBeat>Read the application console</DocBeat>
<DocBeat>Inspect health, logs, and audit history</DocBeat>
</DocSpine>

<DocContract label="Command">agent-up diagnostics</DocContract>

<DocSurfaces>
<DocSurface cli>`agent-up diagnostics` shows process and health state, recent application logs, and relevant JavaScript, network, and browser errors. Entries are marked `active` or `resolved`. `agent-up review-snapshot` writes a JSON snapshot of the current workspace for someone reviewing an agent task.</DocSurface>
<DocSurface mcp>Call Orchestration MCP `get_workspace_console` first after browser failures. If that tool is unavailable, query Audit MCP for recent `application` events from `process`. `export_workspace_review_snapshot` is a shareable snapshot after an agent task, not a substitute for the live workspace.</DocSurface>
</DocSurfaces>

<p><img src="/desktop-diagnostics.png" alt="Desktop diagnostics showing Storefront process events" /></p>
<p><img src="/desktop-console.png" alt="Desktop console showing Storefront install output" /></p>

<DocNext href="/docs/verification" title="Verification">
Required checks before enqueue.
</DocNext>
