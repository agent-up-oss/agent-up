---
title: Design Principles
---

# Design Principles

Agent-Up is built around a small set of constraints that keep it framework agnostic, predictable, and suitable for multiple concurrent AI workspaces.

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
