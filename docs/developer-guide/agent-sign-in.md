# Agent sign-in

Agent-Up signs agents in with a ChatGPT, Cursor, or Claude subscription, never an API key. The
three vendor CLIs implement three different sign-in shapes, and this page describes how the Server
and the clients handle all of them without special-casing any one agent.

## Transports

The Server declares which shape an agent CLI implements and reports it on the sign-in challenge as
`transport`. Clients branch on that and never on which agent is signing in.

| Transport | What happens | Carried back | Works when the browser is elsewhere |
|---|---|---|---|
| `poll` | The CLI prints a deep link and polls the provider itself. | nothing | yes |
| `code` | The user carries a value: either a user code typed into the provider page, or an authorization code pasted back out of it. | a code, sometimes | yes |
| `redirect` | The provider redirects to a loopback address the CLI is listening on. | the redirect URL | only with an in-app WebView |

Defaults per agent kind live in `AgentLoginFlowProvider`, matching the login command
`AgentLoginCommandProvider` runs:

- **Codex** — `codex login --device-auth`, so `code` with a user code.
- **Cursor** — `cursor login`, so `poll`.
- **Claude** — `claude setup-token`, so `code` with a pasted code.

`Agents:{kind}:LoginTransport` overrides this when a deployment points a kind at a different CLI or
a different login subcommand. `Agents:{kind}:LoginChallengeTimeoutSeconds` and
`Agents:{kind}:LoginCompletionTimeoutSeconds` bound each phase, and
`Agents:{kind}:LoginEnvironment` passes anything else a particular CLI build needs.

## Why the Server does not read the flow out of the CLI's output

It used to. That could not work for two of the three shapes:

- `claude setup-token` writes its prompt with **no trailing newline** and then blocks on stdin, so
  a line-oriented read never returns and the link printed above it is never surfaced.
  `AgentLoginOutputReader` reads characters and flushes whatever is pending once the CLI goes
  quiet, which is what makes an unterminated prompt observable.
- There was no way to answer that prompt at all. The Server now keeps stdin open and
  `POST api/workspaces/{id}/agent/login/code` delivers a code to a CLI that is waiting for one.

Output is still parsed for the link, the user code, and the expiry, but the *shape* is declared
rather than guessed, so a code-shaped token in a progress message is never mistaken for a code the
user is meant to type.

## The redirect transport and the callback relay

The agent CLI binds its callback to loopback **on the Server host**. When the browser is on that
same host the redirect reaches the CLI directly and nothing else is needed. When the browser is on
a phone or another machine, the redirect goes nowhere unless something carries it back.

`POST api/workspaces/{id}/agent/login/callback` accepts an intercepted redirect and
`AgentLoginCallbackRelay` replays it against the CLI's listener. The URL comes from a client, so it
is a request-forgery vector and is treated as one: it is accepted only when it matches the redirect
URI the CLI itself advertised — host, port, and path — and only when that address is loopback.

## Clients

`AgentUp.AgentAuth` (`@agent-up/agent-auth`) holds the client half. `resolveAction` turns a
challenge into the single thing to do; `runAction` does it through a platform port. The state
machine has no React or React Native imports, so it is tested under plain Node.

Only one platform can complete a `redirect` sign-in from elsewhere: an in-app WebView, which sees
every navigation its own content makes. `Linking.openURL` hands the URL to Safari or Chrome and
nothing comes back, and a browser cannot read a cross-origin popup's location. The port declares
`canInterceptRedirect` and the web adapter says `false` rather than hanging a sign-in it cannot
finish.

A client must not open a sign-in link the user did not ask it to open.

## Testing

`AgentUp.TestAgents` publishes real CLIs implementing each shape, with a real OAuth identity
provider behind them, so the whole path is exercised without signing in to a real vendor.
`AgentUp.Mobile.E2E` drives them through the real mobile client on an iOS simulator, an Android
emulator, and the installable web build. See the Testing section of `AGENTS.md`.

### Keeping the suites quick

Compiling the client is nearly all of what those jobs cost, and most pushes do not change the
client: they change the Server, the test agents, or the suites. So the compiled app is cached
under a key covering every tracked file that ends up bundled into it - the client, the shared
sign-in module, the design system, the audit package - and a push that touches none of them
reuses it and skips the prebuild, the pods, and the build outright. `mobile-app-key.sh` computes
that key from git's own blob hashes.

**If you add a source tree that gets bundled into the app, add it there too.** A key that does not
cover something the app contains will serve a stale app, and the suite will test the wrong build
without saying so.

The pods and their object files are cached separately, under the dependency set alone, so a change
to the client relinks rather than recompiling every dependency. Both are saved immediately after
the build rather than at the end of the job, because a run whose tests fail would otherwise throw
the build away - which is exactly when the next push is about to need it.
