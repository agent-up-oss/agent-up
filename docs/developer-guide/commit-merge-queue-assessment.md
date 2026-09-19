---
title: Commit Merge Queue Assessment
---

# Commit Merge Queue Assessment

## Decision

The idea is feasible and addresses the main mismatch in the current commit queue: agents naturally produce dependent, incremental states, while the current queue stores independent patches against one unchanged working-tree base. A Git-backed linear queue gives every proposal an exact parent, makes intermediate states reproducible, and creates the right place to attach verification evidence.

It is not a small extension to the existing queue. The useful product is a **Server-owned proposal stack with a separate queue worktree**, not merely a CLI queue whose patch files happen to be commits. Desktop, Mobile, CLI, and MCP can then observe the same state; agents and managed applications run at the queue tip; and the developer's working branch remains unchanged until an entry is approved.

The first implementation adopts this direction behind an optional `commits.enabled` setting. It creates a namespaced ref and private worktree, commits dependent entries there, runs the current Verification gate before enqueue, preserves stable queue metadata, and leaves the developer branch unchanged. Rebase/repair, review application, tree-bound verification receipts, and client parity remain later phases described below; the existing independent patch behavior remains available while that migration is incomplete.

## Why the current model fights incremental work

The present implementation has two parallel owners: a local CLI queue and a Server commit queue. Each stores metadata in a platform configuration directory and stores the content as patch files. Enqueue captures a diff against `HEAD`, restores those files, and prohibits a later entry from owning the same file. That is safe for unrelated vertical slices, but deliberately prevents the sequence described in this proposal:

```text
A: add an interface
B: implement the interface introduced by A
C: update the UI to consume B
```

After A is enqueued, its files disappear from the working tree. B cannot reliably compile against A, and B cannot modify a file assigned to A. The reminder to stop whenever a queue exists reinforces an independent-batch model rather than an incremental stack.

The verification module improved test selection and proof, but it currently reasons about changed bytes in one checkout. Receipts prove that a command ran against a map of file-content hashes. They do not identify a queue generation, base commit, proposal commit, or complete repository tree. Consequently, a receipt for B cannot currently prove that B ran on `base + A + B`, and a base rewrite cannot invalidate all evidence whose unchanged-looking file subset was nevertheless built against a different tree.

These are model limitations, not prompt-quality problems. More instructions to agents will not make independent patches represent dependencies.

## Correct mental model

This feature is closer to a local stacked-change system than to a hosted merge queue.

- A hosted merge queue normally takes already-reviewed changes and tests speculative combinations before updating a protected branch.
- This proposal takes not-yet-reviewed agent changes, preserves their order and dependencies, and advances a human-owned branch one reviewed delta at a time.
- The queue is therefore a **proposal stack**. “Merge queue” remains useful product language because entries advance in order, but implementation and UI text must not imply that approval automatically merges a remote pull request.

An entry need not be standalone relative to the original base. It must be coherent relative to its declared parent: it has one purpose, an inspectable delta, an honest suggested message, and passing checks selected from that delta while executed against the resulting cumulative tree. This is the practical meaning of “verified by itself.” Running B without A would test an impossible state and provide misleading assurance.

## Proposed Git representation

### Stable metadata plus Git objects

Use Git commits for content and ancestry, and a small Server-owned metadata store for workflow state. Do not encode mutable review state only in commit messages, notes, or branch names.

```text
working branch:  W0 ---- W1               (human-owned)
                       /
queue ref:             Q1 ---- Q2 ---- Q3 (agent proposals)
                        A      B      C
```

The queue record needs at least:

- stable queue and entry IDs that survive rewritten commit hashes;
- repository identity, working-branch ref, original base, current base, queue generation, and queue-tip object ID;
- for each entry: parent/tree/commit IDs, proposed message, authoring identity, lifecycle state, review decision, and timestamps;
- the exact changed paths and patch identity derived from `parent..entry`;
- verification plan identity, required check states, receipt IDs, and the tree/config identity they prove;
- conflict or block information and an audit trail of rewrites, approvals, rejection, and repair.

Store the live tip under an Agent-Up namespaced ref such as `refs/agent-up/queues/<queue-id>/tip`. Namespaced refs are less likely than a normal local branch to clutter developer branch lists or be pushed accidentally. Keep reflog or explicit recovery refs long enough to undo queue mutations. A user-visible `agent-up-commits` branch is acceptable for an early spike, but is a weaker production contract because its name collides across worktrees and invites checkout, deletion, and accidental push.

### A separate queue worktree is essential

A ref alone does not let an agent continue from Q1 while leaving the human worktree at W0. The Server should create a private Git worktree for the queue tip and register its lifecycle as part of the workspace:

- agent edits, builds, verification commands, and managed applications run in the queue worktree;
- enqueue commits the selected delta there, updates the queue ref atomically, and leaves the worktree checked out at the new tip for subsequent work;
- the developer's original worktree and branch are not reset, switched, staged, or committed by enqueue;
- clients clearly label whether they are displaying the developer checkout or the proposal-stack preview;
- generated build output and application state remain isolated by the existing workspace/runtime identity rules.

Without this second worktree, the design must choose between hiding A from the agent (breaking dependency), leaving A as uncommitted content on the working branch (breaking isolation), or moving the human checkout onto the queue branch (breaking the promise that the working branch is untouched).

The Server must own this worktree and queue state. The CLI becomes a thin Server client for queue operations. Keeping a second local CLI implementation would reproduce the current split-brain behavior and cannot support consistent visualization in all clients.

### Enqueue transaction

For a clean queue worktree at tip Qn, enqueue should:

1. validate that the queue generation and expected parent still match Qn;
2. identify the proposed delta from Qn to the index/working tree, including renames, modes, binary files, additions, and deletions;
3. apply commit-policy and request validation to that delta;
4. resolve the verification plan from the changed paths;
5. create Qn+1 using Git plumbing or an ordinary commit in the private worktree;
6. run required verification against Qn+1 and record evidence bound to Qn, Qn+1's tree, and the verification configuration;
7. update metadata and the queue ref with compare-and-swap semantics;
8. report the entry as `ready`, `failed`, or `blocked`, while leaving the agent at the cumulative tip.

Whether verification runs before or after the commit object is created is an implementation detail. Creating an unreachable candidate commit first makes the exact tested tree immutable; the public queue ref should advance only according to the selected policy. The recommended first policy allows failed entries to remain visible at the tip, marked failed, so an agent can repair them, but prohibits downstream enqueue until the failed entry is amended. Allowing agents to build on known-failing entries immediately would turn one actionable failure into many ambiguous downstream failures.

Concurrent mutation must be optimistic and generation-based. Filesystem locking remains useful inside one Server process, but an expected tip/generation plus atomic ref update is needed to detect stale clients, multiple Server instances, or external ref changes.

## Verification semantics

### What one entry must prove

For entry B, change selection uses the delta `tree(A)..tree(B)`, while execution occurs in the full checkout for `tree(B)`. A valid receipt must bind at least:

```text
queue generation
entry ID
parent tree ID
result tree ID
verification configuration hash
check ID and normalized command
platform/capability identity where relevant
exit status and completion time
```

The result tree ID is the decisive content address. Recording only hashes for files directly covered by a check is insufficient for a queue rebase: a dependency, generated input, SDK pin, or build configuration elsewhere in the tree can change the result without changing B's own paths. Existing file-level hashes can remain for explanations and selective invalidation, but queue acceptance should require tree-bound evidence.

Test selection remains owned by `AgentUp.Verification`; the commit queue must not grow a second rules engine. The queue asks the Verification controller to plan and run checks for an explicit `(parent tree, result tree, checkout)` target. This preserves the rule that Verification never reads commit-queue internals while allowing the Commits slice to consume verification DTOs through its controller boundary.

The old `commits.build`, `commits.test`, per-project `test`, and entry-level free-form `tests` metadata overlap with the newer verification path rules. A migration should make `verification` the sole authority for acceptance. During compatibility, legacy commit tests may be translated into additional required checks with a visible deprecation warning; silently combining two selection systems would make it hard to explain why an entry is blocked.

### Queue states

Use explicit states rather than a single passing flag:

| State | Meaning |
|---|---|
| `draft` | Content exists, but its verification plan has not completed. |
| `verifying` | Required checks are running for the current generation. |
| `ready` | Required blocking checks have valid receipts for the exact parent/result trees. |
| `failed` | A required check completed unsuccessfully. |
| `conflicted` | The delta cannot be replayed onto its current parent. |
| `stale` | Content or ancestry changed and old receipts no longer prove this entry. |
| `blocked` | An earlier entry is not ready, so this entry cannot be meaningfully advanced. |
| `in-review` | The entry is materialized for human inspection/preview. |
| `approved` | The human accepted content and message; branch integration is pending or in progress. |
| `integrated` | The equivalent delta was committed to the human branch and the queue advanced. |
| `rejected` | The entry is retained for audit/recovery but is no longer on the live stack. |

Only one mutation or verification transition may own a queue generation at a time. Client DTOs should expose both the state and a structured reason, not infer state from missing receipts or command text.

## Human review and integration

### Preview before touching the working branch

The queue worktree already contains the exact cumulative state to run through Agent-Up applications. Human review should therefore begin there: show the entry's parent-to-result diff, message proposal, verification evidence, prior decisions, and a live workspace preview at that entry. There is no need to patch the developer checkout merely to inspect the application.

Older entries require a temporary review worktree or a controlled checkout of their tree, because the main queue worktree normally stays at the tip for the agent. Starting applications for an older tree must use a distinct workspace/runtime identity so it cannot overwrite the tip preview's ports, processes, or browser state.

### Apply a delta, not a commit

On approval, Agent-Up should compute the entry's exact binary-safe delta from its parent and apply it to the developer worktree without creating a commit. “PATCH, not git” should mean “do not cherry-pick or commit,” not “avoid Git's patch machinery.” Git is the most reliable available implementation for rename, mode, binary, index, and three-way safety. A hand-written patch engine would be less correct.

The first version should require the developer worktree to be clean and still at the expected base. It can then apply the delta to the index and working tree, rerun/guard verification there, and present an editable commit message. The human performs the final commit action. Later support for unrelated dirty files can use an explicit path-overlap check and a temporary index, but automatic stash/pop should not be the default because it hides conflicts and can disturb unrelated work.

Approval is not complete until all of these are true:

1. the delta applies without conflict to the current human `HEAD`;
2. verification selected from that delta passes or has valid receipts for the exact integrated tree;
3. the human confirms the final message and commit;
4. the Server observes the resulting commit and records its mapping to the stable queue entry ID.

Do not pop an entry when it is merely staged. A crash, message edit, hook failure, or human reset between staging and commit must leave a recoverable `integration-pending` state.

### Rejection and partial acceptance

Rejection must preserve work, but it cannot promise that downstream content remains meaningful. Offer separate decisions:

- **request changes** keeps the entry and its descendants, marks the entry stale/failed, and opens a repair session at that point;
- **drop** archives the entry and attempts to replay every descendant onto its parent;
- **replace/amend** creates a new version of the entry, then replays and reverifies descendants;
- **split** returns selected hunks/files to a draft successor rather than discarding them;
- **accept with message edit** changes workflow metadata without rewriting content until the final human commit.

If A is dropped and B semantically depends on A, Git may replay B cleanly even though B is now wrong. Verification can catch many such cases but not prove intent. The UI must label clean replay as mechanical success, require downstream reverification, and leave semantic review to the human/agent. No design can safely automate that judgment from Git topology alone.

## Rebase and upstream-change recovery

Call the operation **rebase queue** in the product, but implement it as a Server-controlled replay in a temporary worktree rather than exposing an interactive Git rebase to clients.

Given a new base W1:

1. snapshot the live queue generation and create a temporary recovery ref;
2. replay Q1's parent-to-result delta onto W1;
3. if it applies, create Q1', invalidate its old tree-bound receipts, resolve its plan, and verify it;
4. stop at the first conflict or failed required check;
5. expose a repair worktree for that stable entry ID;
6. after repair, create the replacement entry and resume Q2, Q3, and so on in order;
7. publish the new queue tip and metadata atomically, retaining the old generation for rollback/audit.

Each changed ancestor necessarily changes descendant parent and result tree IDs, so every downstream entry becomes stale even when its textual patch is unchanged. Checks can be cached only when the verification subsystem can prove that its stronger tree/environment cache key is identical; the safe initial behavior is ordered reverification.

The live queue should not be left half-rewritten. Metadata may describe a candidate generation and its failure, while the last complete generation remains recoverable. If product requirements favor showing successfully rebuilt prefixes immediately, model the candidate and active generations explicitly rather than moving one ref piecemeal.

External Git operations still need a guard, but no longer require users to discard the queue. A merge or rebase in the human worktree pauses integration and enqueue publication; once it finishes, Agent-Up detects that the base changed and offers the ordered queue rebase.

## Server and client architecture

### Ownership

Keep `AgentUp.CommitPolicy` as the stateless message/file-classification library. Move authoritative queue lifecycle into `AgentUp.Server/Features/Commits`. Add Git-object/worktree/replay providers there, while services own queue transitions and call Verification only through a controller boundary.

The existing CLI `Commits` implementation should become a compatibility adapter and then a thin REST client. A Server-independent fallback is incompatible with all-client visualization, shared locks, live verification progress, managed preview applications, and a single authoritative queue generation.

Persistence should be durable Server state plus Git refs, not just Git refs. Git owns content and ancestry; Server persistence owns stable IDs, decisions, receipt associations, active/candidate generations, and recovery information. Every mutation emits audit events.

### API and MCP surface

All clients need the same versioned DTOs and event stream. A representative REST surface is:

```text
GET    /api/workspaces/{workspaceId}/commit-queue
POST   /api/workspaces/{workspaceId}/commit-queue/entries
GET    /api/workspaces/{workspaceId}/commit-queue/entries/{entryId}
POST   /api/workspaces/{workspaceId}/commit-queue/entries/{entryId}/verify
POST   /api/workspaces/{workspaceId}/commit-queue/entries/{entryId}/edit
POST   /api/workspaces/{workspaceId}/commit-queue/entries/{entryId}/review
POST   /api/workspaces/{workspaceId}/commit-queue/rebase
POST   /api/workspaces/{workspaceId}/commit-queue/integrations
```

The MCP endpoint should expose status, diff, enqueue, verification, repair, and rebase operations. Human approval and final commit remain unavailable to agents by default. MCP can request a review, explain blockers, fix a selected entry, or resume ordered verification; it cannot convert its own proposal into a human commit. Every mutating call includes the expected generation.

Desktop, Mobile, and CLI should show:

- base and tip identity, generation, queue health, and currently running operation;
- an ordered dependency graph (linear initially) with per-entry state;
- diff, proposed message, verification plan/results, logs, and stale reasons;
- live preview selection and whether it is the tip or an older isolated review workspace;
- explicit request-changes, drop, repair, rebase, approve/apply, and recovery actions according to client capability.

Mobile may support review decisions, but final integration into a local developer worktree is only available when the Server owning that worktree is connected and can prove its state. The UI must never optimistically display an entry as integrated.

## Optional configuration

A narrow opt-in is preferable:

```json
{
  "commits": {
    "enabled": true
  },
  "verification": {
    "enforcement": "block"
  }
}
```

Suggested semantics:

- missing `commits` or `enabled: false`: no queue worktree, refs, routes in navigation, agent reminders, or background checks;
- `enabled: true`: the Server offers the proposal queue, but does not create a worktree until the first enqueue;
- enabling commits requires a valid `verification` configuration for production use; the prototype may permit warning mode but must label entries as unverified rather than ready;
- disabling with a non-empty queue is rejected until the user exports, integrates, or archives it;
- queue storage location, ref names, and worktree paths are implementation details, not repository-controlled strings;
- verification concurrency and resource limits belong to Server/operator configuration unless there is a clear repository-level need.

This deliberately does not preserve `commits.projects` as the future contract. Verification path rules already own check selection and should be the only definition source once migration completes.

## Principal risks and mitigations

| Risk | Severity | Mitigation |
|---|---:|---|
| Agent and human accidentally edit different checkouts | High | Prominent checkout/base/tip labels, absolute paths in tool responses, Server routing by workspace ID, and no silent workspace switching. |
| Upstream rejection replays cleanly but breaks intent | High | Ordered reverification, semantic review warning, request-changes as the default, and no claim that Git conflict freedom means correctness. |
| Verification cost grows with queue length | High | Stop at first failure, deduplicate commands within one entry, tree-keyed caching only when sound, cancellable jobs, and visible resource estimates. |
| A dirty or advanced human branch makes approval unsafe | High | Clean-tree/expected-base requirement first; explicit rebase before apply; no automatic stash. |
| Queue refs or metadata diverge after a crash | High | Atomic ref compare-and-swap, write-ahead operation records, recovery refs, startup reconciliation, and idempotent transitions. |
| Git hooks/signing differ between proposal commits and final commits | Medium | Proposal commits are explicitly synthetic and need not be signed; final human commit runs normal hooks/signing and is not marked integrated until observed. |
| Secrets or local build artifacts enter proposal commits | High | Preserve ignore rules, explicit file selection, sensitive-path policy, diff review, and never use blanket `git add -A` without showing the selected delta. |
| Binary/LFS/submodule/sparse-checkout behavior is incorrect | High | Declare an initial support matrix and test each feature; fail closed for unsupported repository modes. |
| Old clients mutate a newer queue incorrectly | High | Versioned DTOs, expected-generation preconditions, capability discovery, and structured conflict responses. |
| A normal branch name leaks or is pushed | Medium | Use namespaced refs, keep queue refs out of default push refspecs, and provide explicit export rather than relying on branch discovery. |

## Feasibility by capability

| Capability | Feasibility | Notes |
|---|---|---|
| Linear dependent entries in Git | High | Native commit/tree ancestry is a much better representation than independent patch files. |
| Keep the developer branch untouched while agents continue | High | Requires a private queue worktree; infeasible with only the current checkout. |
| Verify every entry in context | High | Requires explicit-tree verification targets and stronger receipts, but fits the current Verification boundaries. |
| Ordered rebase with repair | Medium-high | Git supplies replay mechanics; durable generations, repair UX, and crash recovery are substantial work. |
| Patch an approved delta into the working branch | High for clean trees | Dirty-tree composition and submodules/LFS should be deferred or constrained. |
| Preserve downstream work after rejection | Medium | Mechanically replayable, never semantically guaranteed. This limitation must be visible. |
| Full Desktop/Mobile/CLI/MCP visualization | High | Straightforward once Server ownership and evented DTOs exist; broad implementation and test scope. |
| Reuse current patch queue in place | Low | Current independent ownership, file exclusivity, patch persistence, and stop-on-queue workflow conflict with the core model. |

Overall feasibility is **high**, with **high implementation scope and medium operational risk**. The Git operations are not the hardest part. Correct ownership, exact verification identity, multi-worktree UX, recoverable state transitions, and honest handling of downstream semantics are the real engineering work.

## Delivery plan

### Phase 0: repository spike

Build a disposable provider-level spike, not a user-facing feature. It must prove on Linux, macOS, and Windows:

- namespaced ref and private-worktree creation/removal;
- sequential commits touching the same file;
- additions, deletions, renames, executable modes, binary files, and paths with spaces;
- apply-to-clean-worktree without committing;
- base advancement, conflict at entry N, repair, and continuation;
- crash recovery between candidate commit creation, metadata write, and ref publication;
- detection of worktrees, submodules, LFS, sparse checkout, case-only renames, and unsupported repository states.

Exit with a written support matrix and measured queue/rebase cost on a representative repository.

### Phase 1: Server-owned linear queue

- Introduce durable queue generations, stable entry IDs, namespaced refs, and the managed queue worktree in the Server Commits slice.
- Support enqueue, status, inspect, diff, edit/amend, archive, and recovery with expected-generation concurrency.
- Keep human integration disabled initially.
- Add controller, provider, HTTP, MCP, crash-recovery, and architecture tests in the matching test projects.

The current Phase 1 implementation also treats an existing dependent queue as continuation state rather than a guard failure. `guard_commits` returns the managed worktree path, and `enqueue_commit` returns the same path in structured MCP result data. Legacy independent-patch queues remain blocking. Transport coverage must exercise these fields through `/mcp/commits`; calling the C# tool adapter directly is not sufficient protocol coverage.

### Phase 2: tree-targeted verification

- Extend Verification controllers/DTOs to accept explicit parent/result tree targets and an execution checkout without learning queue internals.
- Bind receipts to result tree, parent, configuration, command, and relevant environment identity.
- Implement ordered execution, first-failure blocking, cancellation, stale propagation, and progress events.
- Deprecate commit-specific test selection in favor of verification rules.

### Phase 3: review and clean-tree integration

- Add Desktop review UI first, including live queue-tip preview, diff, message editing, evidence, request changes, and clean-tree apply.
- Require final verification and human commit confirmation; reconcile the resulting commit before advancing.
- Add rollback/recovery for interrupted integrations and hooks that reject the final commit.

### Phase 4: rebase and downstream repair

- Add candidate generations, temporary replay worktrees, stop-at-first-failure behavior, repair sessions, and full downstream reverification.
- Add drop, replace, and split only after their audit and recovery semantics are tested.
- Expose safe repair/rebase operations through MCP while retaining human-only approval.

### Phase 5: client parity and migration

- Make CLI a thin Server client and remove duplicated queue persistence after an explicit migration/export window.
- Add Mobile read/review views and Server event streaming.
- Enable `commits.enabled` as the opt-in contract only after package upgrades, Server restarts, orphan cleanup, and old-client behavior are proven.

## Go/no-go acceptance criteria

Proceed beyond the spike only if all of the following can be demonstrated:

1. The human branch and index remain byte-for-byte unchanged through enqueue, agent continuation, verification, Server restart, and queue rebase.
2. Every displayed entry reconstructs exactly from Git objects and has a stable ID across rewrites.
3. Verification for entry N selects from `N-1..N`, runs on N's full tree, and becomes stale after any ancestor rewrite.
4. A crash at every mutation boundary recovers to either the old complete generation or the new complete generation, never a mixed queue.
5. Approval never creates a commit, silently stashes work, or removes an entry before the final human commit is observed.
6. Conflict repair resumes at the failed entry and reprocesses every descendant in order.
7. Desktop, CLI, Mobile, and MCP report the same generation and states, and stale clients receive a structured concurrency failure.
8. Disabling or uninstalling the feature cannot garbage-collect unrecovered proposals without an explicit retention period and warning.

If the separate-worktree UX proves too confusing, the fallback should be a simpler Server-owned independent queue with stronger verification—not a hidden cumulative stack in the developer's working tree. The latter would retain the ambiguity this proposal is intended to remove.
