---
title: Developer Guide
slug: /
---

# Developer Guide

<DocWhat>
This guide captures the implementation direction for Agent-Up. The Server owns orchestration and runtime state. Desktop, CLI, and MCP clients stay thin.

Agent-Up is a workspace manager, not an application framework. Every implementation decision should preserve framework agnosticism, zero application source changes, and multiple concurrent isolated workspaces.
</DocWhat>

<DocFacts label="MCP servers">
<DocFact label="orchestration">/mcp/orchestration</DocFact>
<DocFact label="browser">/mcp/browser</DocFact>
<DocFact label="audit">/mcp/audit</DocFact>
<DocFact label="commits">/mcp/commits</DocFact>
<DocFact label="verification">/mcp/verification</DocFact>
</DocFacts>

MCP is a protocol, not a slice. Each slice General lists that slice's tools and routes.

## Start here

- [Repo](/developer-guide/repo/architecture) covers architecture rules, packaging, CI, `au-debug`, design-system consumption, and telemetry.
- [Testing](/developer-guide/repo/testing) sets out how tests are written: builders, shared domain data, and setup that stays in the test.
- [Mobile store release](/developer-guide/repo/mobile-store-release) documents Mobile CI smoke builds and the dispatched Android and iOS store pipeline.
- Slice Generals use the same labels as User Docs: [Workspaces](/developer-guide/workspaces), [Applications](/developer-guide/applications), [Git](/developer-guide/git), [Commits](/developer-guide/commits), [Agents](/developer-guide/agents), [Browser](/developer-guide/browser), [Diagnostics](/developer-guide/diagnostics), [Verification](/developer-guide/verification), [Configuration](/developer-guide/configuration).

The [Commit merge queue assessment](/developer-guide/commits/merge-queue-assessment) and [Desktop application hosting assessment](/developer-guide/applications/hosting-assessment) are design notes, not the current contract.
