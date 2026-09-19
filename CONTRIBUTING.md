# Contributing

Agent-Up is an experimental development preview. Interfaces, configuration, and workflows may change while the project is still finding the right shape.

## Before Opening a Pull Request

- Prefer small, focused changes.
- Discuss large architectural changes in an issue or discussion first.
- Keep implementation, `AGENTS.md`, and docs synchronized when behavior, architecture, workflows, configuration, or public contracts change.
- Follow the server-first architecture: `AgentUp.Server` owns orchestration; Desktop, CLI, MCP clients, and future integrations are clients.
- Add or update tests for production behavior changes.
- Avoid unrelated refactors in feature work.

## Local Checks

Prefer the `au-debug` wrappers from `AGENTS.md`. After production changes:

```bash
./au-debug test architecture
```

When `AgentUp.Mobile/` changed:

```bash
./au-debug test mobile
```

Do not invoke the wrapped `dotnet test` or Mobile npm commands directly when an `au-debug` wrap exists. Docs:

```bash
npm --prefix docs install
npm --prefix docs run build
```

## Contribution License

By contributing, you agree that your contribution is licensed under the Apache License 2.0.
