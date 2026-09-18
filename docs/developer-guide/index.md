---
title: Developer Guide
slug: /
---

# Developer Guide

This guide captures the implementation direction for Agent-Up.

Agent-Up is a workspace manager, not an application framework. The Server owns orchestration and runtime state. Desktop, CLI, and MCP clients stay thin. Every implementation decision should preserve framework agnosticism, zero application source changes, and multiple concurrent isolated workspaces.

MCP is a protocol, not a slice. Attach to `/mcp/orchestration`, `/mcp/browser`, `/mcp/audit`, `/mcp/commits`, and `/mcp/verification`. Each slice General lists that slice's tools and routes.

## Start here

- [Repo](/developer-guide/repo/architecture) covers architecture rules, packaging, CI, `au-debug`, design-system consumption, and telemetry.
- Slice Generals use the same labels as User Docs: [Workspaces](/developer-guide/workspaces), [Applications](/developer-guide/applications), [Git](/developer-guide/git), [Commits](/developer-guide/commits), [Agents](/developer-guide/agents), [Browser](/developer-guide/browser), [Diagnostics](/developer-guide/diagnostics), [Verification](/developer-guide/verification), [Configuration](/developer-guide/configuration).

The [Commit merge queue assessment](/developer-guide/commits/merge-queue-assessment) and [Desktop application hosting assessment](/developer-guide/applications/hosting-assessment) are design notes, not the current contract.
