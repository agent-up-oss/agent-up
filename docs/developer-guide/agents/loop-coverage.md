---
title: Loop coverage
---

# Loop coverage

This page is the contract for running the Server loop kernel against a real token stream. Hard invariants fail the run. Outcome scores are pooled against a versioned baseline. The same scenario files drive CPU inference on every change and a scripted ACP agent on demand.

<DocFacts>
<DocFact label="Check">loop-inference</DocFact>
<DocFact label="Category">LoopInference</DocFact>
<DocFact label="Agent">test-agent-infer</DocFact>
<DocFact label="Model">Qwen2.5-1.5B-Instruct Q4_K_M</DocFact>
</DocFacts>

## Kernel

The Server loop attaches an optional permission policy, iterates `PromptWaitAsync`, and stops in one typed terminal: `Completed`, `EndTurn`, `Cancelled`, `MaxIterations`, `BlockedPolicy`, `BlockedAwaitingInput`, `BlockedRateLimited`, `BlockedPrecondition`, `BlockedHostUnavailable`, `Failed`, or `Idle`. A null policy is today's human path. An unmatched permission is `blocked:policy` plus a reject option so the agent does not hang. Bind, policy HTTP, roles, and assigned-issue activation live on [Autonomy](/developer-guide/agents/autonomy); this kernel is the subject the CPU-inference gate drives.

## Invariants

These fail the run even when the pooled outcome score is inside tolerance:

- The permission queue is empty.
- The prompt gate is released.
- The transcript has at most 256 events.
- The terminal and stop reason are typed.
- Every resolved permission records a rule id.
- `Completed` recorded a completed task.
- `BlockedPolicy` recorded a block reason.

## CPU inference

`test-agent-infer` is an ACP v1 stdio agent in `AgentUp.TestAgents`. It loads the SHA-256-pinned GGUF through LLamaSharp's CPU backend. LLamaSharp is not a dependency of Server, Desktop, or CLI.

Spike on 12 Oct 2026: Qwen2.5-0.5B-Instruct Q4_K_M parsed tagged tool calls on 0/10 strict runs and 11/16 with a generous parser. Qwen2.5-1.5B-Instruct Q4_K_M parsed tagged JSON on 8/8 single-step prompts at about 1.5s per generation on 4 CPU threads. The gate pins the 1.5B weights.

SHA-256: `6a1a2eb6d15622bf3c96857206351ba97e1af16c30d7a74ee38970e434e9407e`. A cache miss downloads from the documented Hugging Face mirror. A SHA mismatch deletes the file and fails.

The process prints `loop-inference seed=` on stderr. Replay a failure with `AGENTUP_LOOP_INFERENCE_SEED`. The default seed is 42.

## Scenarios

Reusable prompts live in `loop-coverage/scenarios/*.json`. They do not name an agent. `scoresOutcome` scenarios contribute to the pooled rate in `loop-coverage/baselines/cpu-inference.json`. Invariant-only scenarios still fail on a broken kernel.

`test-agent-scripted` consumes the same files. Do not fork scenario JSON per agent. `loop-scripted` is the fast check over that agent's declared-turn script.

## CI

`./.github/scripts/run-loop-inference.sh` publishes `AgentUp.TestAgents`, verifies the pinned GGUF, and runs `AgentUp.Server.Tests` with `Category=LoopInference`. The Loop CPU inference job always runs and gates the release. It is `slow` and `ciOnly`. It is excluded from the test-kind watchdog, `run-dotnet-tests.sh`, and the fast `server` check.

`Category=LoopSoak` is a separate `slow` `ciOnly` check. `./.github/scripts/run-loop-soak.sh` runs three concurrent assigned-issue loops and proves a restart does not silently resume. It is excluded from the same fast suites and also gates the release.

<DocNext href="/developer-guide/agents/sign-in" title="Sign-in">
Poll, code, and redirect transports.
</DocNext>
