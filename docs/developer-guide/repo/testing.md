---
title: Testing
---

# Testing

Agent-Up's suite is large and fast, and both of those are worth keeping. What follows is how
tests are written so that stays true: a signature change edits one file rather than a hundred
call sites, and reading a test tells you what its subject was given.

`AgentUp.Verification.Tests` was written to this standard deliberately and is the worked
example to copy. The rules below are enforced by `AgentUp.Architecture.Tests`, so this page
describes what the build already checks rather than a convention to remember.

## Test data goes through builders

Production DTOs and models are not constructed in tests. Each test project keeps a builder
per coupling hotspot in its `Support/` folder:

```csharp
var workspace = ServerDomain.Workspace()
    .Named("Shop")
    .WithApplication(new ApplicationDefinitionBuilder("Api", "dotnet run").WithPort(8080))
    .Build();
```

A builder starts from a sensible default and lets a test state only the attribute it is
actually about. `RegisterWorkspaceRequest` has five positional fields and five collection
properties; adding one now changes `RegisterWorkspaceRequestBuilder` rather than the hundred
registrations across the suite.

`TestDataBuilders` enforces this in two halves:

- A production DTO or model under `Features/*/DTOs` or `Features/*/Models`, with more than
  three constructor parameters, constructed more than fifteen times in one test project,
  must have a `Support/<Type>Builder.cs` in that project. A narrower type reads fine
  positionally and is left alone; the threshold catches a type as it becomes a hotspot.
- Once that builder exists, nothing else in the project constructs the type, including
  through an implicit `new(...)`, which is judged by the type it is written into.

## Domain vocabulary is shared and named

Each test project has one file naming its world: `ServerDomain`, `CliDomain`,
`DesktopDomain`, `DebugDomain`, `VerificationDomain`. One canonical workspace, one
application, one port, one queued commit, named once and reused.

```csharp
Assert.That(overview.WorktreePath, Is.EqualTo(ServerDomain.WorktreePath));
```

Tests refer to those names rather than inventing path, branch and commit strings, so a
reader can tell at a glance whether two tests are talking about the same workspace or
different ones. Where a test needs one value to be specific, it injects that value through a
builder instead of restating the whole world.

## Variation is a call, not a new fixture method

Canned fixtures fail at the first test that needs "the same thing, but different". A builder
returned from the domain gives that test a seam:

```csharp
DesktopDomain.Workspace().Stopped().Build();
DesktopDomain.WorkspaceServing(port: 10200).Build();
```

Domain helpers that stand for a shape - `WorkspaceServing`, `WorkspaceWithApplications` -
return a builder rather than a finished object, so the next variation does not need another
method on a shared file.

## Setup lives in the test

New fixtures arrange their subject inside the test, through a local helper the test calls
with explicit parameters:

```csharp
private static async Task<(AppDriver App, ItemsControl Transcript)> OpenTranscriptWithAThoughtAsync()
```

A `[SetUp]` method moves the arrangement out of the test that depends on it, and every test
in the fixture then pays for whatever the slowest one needed. `TestFixtureSetup` bans
`[SetUp]` and `[OneTimeSetUp]` in new fixtures, over a ratchet baseline of the fixtures that
predate the rule: entries come out as the setup moves, and the rule fails on an entry that is
already clean, so the file cannot drift into fiction. `[TearDown]` is untouched - releasing a
temp directory says nothing about what a test can be read to mean.

## Composition helpers compose services, not data

`AgentUp.Server.Tests/Fake/ServerTestComposition.cs` assembles the object graphs that are too
large to build at every test. It composes services only; test data comes from the builders.
Every parameter that changes behaviour under test - the configuration provider, the identity
provider, the platform branch - is required rather than defaulted behind a `??`, so reading a
call still tells you what the subject was given. Where a plain composition is common it gets
a named overload that states what it stands for, rather than a null-coalescing default that
hides the choice.

## One behaviour per test

`TestAssertionDensity` caps a test method at ten assertions. Roughly three quarters of the
suite already carries one to three, which is healthy; the cap exists to catch the tail. A
method asserting twenty or thirty things names one behaviour and covers several, so a failure
does not say what broke and the method can only grow.

Splitting usually means either `[TestCase]` per instance of the same rule:

```csharp
[TestCase("DISPLAY", ":123")]
[TestCase("GDK_BACKEND", "x11")]
public async Task Prepare_putsTheHostedDisplayIntoTheApplicationEnvironment(string name, string expected)
```

or one test per concern over a shared local helper. `Assume.That` counts towards the cap. Neither
`Assume.That` nor `Assert.Ignore` may decide at runtime that platform, privilege, or live system
state makes a test inapplicable: that reports success without having run. Cover those branches
through an injected capability provider instead; pre-existing runtime skips are held in a ratchet
baseline that rejects additions and stale entries.

## Rules assert properties, not membership

An architecture rule that pins an exact set fails on any legitimate addition while verifying
no behaviour. `ReleasePayloadLayout` used to assert the payload directories were exactly
`cli, desktop, installer, server, tray`; it now asserts what CI depends on - that every
payload directory is non-empty and unique - which a new payload satisfies and a colliding one
does not.
