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
- [MCP](./mcp.md) covers the automation interface.
- [Event Recording](./event-recording.md) and [Playwright Generation](./playwright.md) describe validation and test generation.
- [CI Configuration](./ci-configuration.md) documents repository secrets and variables for signing and release.
