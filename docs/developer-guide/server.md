---
title: Server
---

# AgentUp.Server

`AgentUp.Server` owns all runtime state and performs all orchestration.

## Responsibilities

The Server manages:

- Workspace registry.
- Process lifecycle.
- Port allocation.
- Docker lifecycle.
- Browser lifecycle.
- Browser profiles.
- Browser session persistence.
- Event recording.
- Diagnostics.
- Health monitoring.
- Playwright generation.
- MCP server.
- REST API.

## Single Source of Truth

Desktop, CLI, and MCP clients connect to the Server. They do not keep authoritative copies of workspace state.

This rule keeps concurrent agents, human developers, and automation clients aligned around the same running environment.

## Orchestration Rule

If a feature starts, stops, restarts, navigates, records, allocates, diagnoses, or exports workspace behavior, that logic belongs in the Server.

## Tutorial Cleanup

`POST /api/workspaces/tutorial/cleanup` is a Desktop onboarding support endpoint. It stops and removes every registered workspace when the first-run tutorial starts, so stale workspace state cannot render behind onboarding or affect the guided sample setup.
