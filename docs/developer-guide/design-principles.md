---
title: Design Principles
---

# Design Principles

Agent-Up is built around a small set of constraints that keep it framework agnostic, predictable, and suitable for multiple concurrent AI workspaces.

Product UI, documentation, and marketing use the repository-owned
[`@agent-up/design-system`](./design-system.md). Canonical HTML and CSS compile to
React Native objects and Avalonia resources and styles. Desktop is the reference
rendering for the quiet black, neutral-structure, semantic-green visual language.
Mobile, docs, and marketing apply those same compiled components; they do not
paint a parallel theme from the palette.

## Framework Agnostic

Agent-Up must support arbitrary web applications. It must not contain framework-specific knowledge about ASP.NET, Spring, React, Next.js, Angular, Vue, Express, or any other application stack.

Applications are described declaratively through configuration and launched as external processes.

## Cross Platform

Agent-Up targets Windows, Linux, macOS, and NixOS.

The desktop application should therefore be implemented with Avalonia so the same UI architecture can run across supported platforms.

## Zero Application Changes

Applications should never reference Agent-Up.

There is no SDK, package, runtime dependency, or source-code integration. Agent-Up supplies runtime configuration through environment variables during launch.

## Server-Owned State

The Server owns all orchestration and runtime state. Desktop, CLI, and MCP clients observe or request changes through the Server.

No orchestration logic belongs in UI, CLI, or automation clients.

## Multiple Concurrent Workspaces

Every feature must assume multiple workspaces can run at the same time. Ports, browser profiles, Docker infrastructure, processes, diagnostics, and application state must be isolated by workspace.
