---
title: Developer Guide
slug: /
---

# Developer Guide

This guide captures the implementation direction for Agent-Up.

Agent-Up is a workspace manager, not an application framework. The Server owns orchestration and runtime state. Desktop, CLI, and MCP clients stay thin. Every implementation decision should preserve framework agnosticism, zero application source changes, and multiple concurrent isolated workspaces.

## Start Here

- [Design Principles](./design-principles.md) defines the constraints that shape the implementation.
- [Architecture](./architecture.md) explains the major components and ownership boundaries.
- [Server](./server.md) describes the single source of truth.
- [Desktop](./desktop.md) and [Mobile](./mobile.md) are the human clients.
- [Packaging](./packaging.md) covers installers and native packages.
- [AUDebug](./au-debug.md) hosts repo Desktop, Mobile, and docs for visual inspection.
- [MCP](./mcp.md) and [Verification](./verification.md) cover the automation and check surfaces.
- [Design System](./design-system.md) is the UI contract.
- [Product telemetry](./telemetry.md) describes Sentry error reporting for Agent-Up processes.
- [Event Recording](./event-recording.md) and [Playwright Generation](./playwright.md) describe validation and test generation.
- [CI Configuration](./ci-configuration.md) documents repository secrets and variables for signing and release.

The [Commit Merge Queue Assessment](./commit-merge-queue-assessment.md) and [Desktop Application Hosting Assessment](./desktop-app-hosting-assessment.md) are design notes, not the current contract.
