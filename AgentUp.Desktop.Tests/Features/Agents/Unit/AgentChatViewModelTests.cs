using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.CompilerServices;
using AgentUp.Desktop.Features.Agents.Controllers;
using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Agents.Interfaces;
using AgentUp.Desktop.Features.Agents.Services;
using AgentUp.Desktop.Features.Agents.ViewModels;
using AgentUp.Desktop.Features.Capabilities.Controllers;
using AgentUp.Desktop.Features.Capabilities.DTOs;
using AgentUp.Desktop.Features.Capabilities.Interfaces;
using AgentUp.Desktop.Features.Capabilities.Services;
using AgentUp.Desktop.Shared.Providers;
using AgentUp.Desktop.Tests.Support;
using ReactiveUI;

namespace AgentUp.Desktop.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class AgentChatViewModelTests
{
    [Test]
    public async Task LoadAndResumeExposeWorkspaceSessionHistory()
    {
        var saved = new AgentSessionSummaryDto("saved-1", "Claude", "Generated description", "feature/agents", DateTimeOffset.UtcNow);
        var initial = DesktopDomain.Session().WithSessions(saved).Build();
        var resumed = initial with { Agent = "Claude", State = "ready", SessionId = "saved-1" };
        var fake = new ChatApiFake { Session = initial, NextSession = resumed };
        var view = CreateView(fake);

        await view.LoadAsync("ws-1");
        await view.ResumeSessionCommand.Execute("saved-1").FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(view.Sessions.Single().SessionId, Is.EqualTo("saved-1"));
            Assert.That(view.Sessions.Single().ActionLabel, Is.EqualTo("Current · ready"));
            Assert.That(view.HasSession, Is.True);
            Assert.That(view.SessionId, Is.EqualTo("saved-1"));
            Assert.That(fake.ResumedSessionId, Is.EqualTo("saved-1"));
            Assert.That(view.Sessions.Single().AutomationId, Is.EqualTo(DesktopAutomationIds.AgentSession("saved-1")));
        });
    }

    [Test]
    public void PermissionOption_usesAStableAutomationId()
    {
        var option = new AgentOptionViewModel("allow-once", "Allow once", ReactiveCommand.Create(() => { }));

        Assert.That(option.AutomationId, Is.EqualTo(DesktopAutomationIds.AgentOption("allow-once")));
    }

    [Test]
    public async Task LoadAsync_labelsTheLiveSessionCurrentAndOthersResume()
    {
        var current = new AgentSessionSummaryDto("demo-agent", "Codex", "Restock featured products", "main", DateTimeOffset.UtcNow);
        var prior = new AgentSessionSummaryDto("demo-prior", "Claude", "Tighten checkout copy", "main", DateTimeOffset.UtcNow);
        var fake = new ChatApiFake
        {
            Session = DesktopDomain.Session()
                .Ready("Codex", "demo-agent")
                .WithSessions(current, prior)
                .Build()
        };
        var view = CreateView(fake);

        await view.LoadAsync("ws-1");

        Assert.Multiple(() =>
        {
            Assert.That(view.Sessions[0].ActionLabel, Is.EqualTo("Current · ready"));
            Assert.That(view.Sessions[1].ActionLabel, Is.EqualTo("Resume session →"));
        });
    }

    [Test]
    public async Task LoadAsync_hidesExpectedAuthenticationRequiredFromTheErrorBanner()
    {
        var fake = new ChatApiFake
        {
            Session = DesktopDomain.Session()
                .WithAgent("Claude")
                .InState("authentication_required")
                .WithError("""{"code":-32000,"message":"Authentication required"}""")
                .IdleWith(DesktopDomain.AvailableAgent("Claude"))
                .Build()
        };
        var view = CreateView(fake);

        await view.LoadAsync("ws-1");

        Assert.Multiple(() =>
        {
            Assert.That(view.Error, Is.Null);
            Assert.That(view.ActivityLabel, Is.EqualTo("Waiting for sign-in"));
        });
    }

    [Test]
    public async Task SubmitLoginCodeCommand_postsThePastedCode()
    {
        var fake = new ChatApiFake
        {
            Session = DesktopDomain.Session()
                .WithAgent("Claude")
                .InState("authenticating")
                .WithLoginChallenge(new AgentLoginChallengeDto(
                    "https://claude.ai/oauth/authorize",
                    null,
                    "Paste the code",
                    AgentLoginTransport.Code,
                    true))
                .IdleWith(DesktopDomain.AvailableAgent("Claude"))
                .Build()
        };
        var view = CreateView(fake);

        await view.LoadAsync("ws-1");
        view.LoginCodeInput = " code-from-the-page ";
        await view.SubmitLoginCodeCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(view.CanSubmitLoginCode, Is.True);
            Assert.That(fake.SubmittedCode, Is.EqualTo("code-from-the-page"));
        });
    }

    [Test]
    public async Task SubmitLoginCodeCommand_ignoresABlankPaste()
    {
        var fake = new ChatApiFake
        {
            Session = DesktopDomain.Session()
                .WithAgent("Claude")
                .InState("authenticating")
                .WithLoginChallenge(new AgentLoginChallengeDto(
                    "https://claude.ai/oauth/authorize",
                    null,
                    "Paste the code",
                    AgentLoginTransport.Code,
                    true))
                .IdleWith(DesktopDomain.AvailableAgent("Claude"))
                .Build()
        };
        var view = CreateView(fake);

        await view.LoadAsync("ws-1");
        await view.SubmitLoginCodeCommand.Execute().FirstAsync();

        Assert.That(fake.SubmittedCode, Is.Null);
        Assert.That(view.CanCollectLoginCode, Is.True);
    }

    [Test]
    public async Task LoadAsync_doesNotCollectADeviceCodeChallenge()
    {
        var fake = new ChatApiFake
        {
            Session = DesktopDomain.Session()
                .WithAgent("Codex")
                .InState("authenticating")
                .WithLoginChallenge(new AgentLoginChallengeDto(
                    "https://auth.openai.com/device",
                    "ABCD-EFGHI",
                    "Enter this code",
                    AgentLoginTransport.Code))
                .IdleWith(DesktopDomain.AvailableAgent())
                .Build()
        };
        var view = CreateView(fake);

        await view.LoadAsync("ws-1");
        view.LoginCodeInput = "not-used";
        await view.SubmitLoginCodeCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(view.CanCollectLoginCode, Is.False);
            Assert.That(view.CanSubmitLoginCode, Is.False);
            Assert.That(fake.SubmittedCode, Is.Null);
        });
    }

    [Test]
    public async Task LoadAsync_hidesABareMinus32000Notice()
    {
        var fake = new ChatApiFake
        {
            Session = DesktopDomain.Session()
                .WithAgent("Claude")
                .InState("authentication_required")
                .WithError("JSON-RPC -32000")
                .IdleWith(DesktopDomain.AvailableAgent("Claude"))
                .Build()
        };
        var view = CreateView(fake);

        await view.LoadAsync("ws-1");

        Assert.That(view.Error, Is.Null);
        Assert.That(view.ActivityLabel, Is.EqualTo("Waiting for sign-in"));
    }

    [Test]
    public async Task LoadAsync_hidesABlankAuthenticationNotice()
    {
        var fake = new ChatApiFake
        {
            Session = DesktopDomain.Session()
                .WithAgent("Claude")
                .InState("authentication_required")
                .WithError("   ")
                .IdleWith(DesktopDomain.AvailableAgent("Claude"))
                .Build()
        };
        var view = CreateView(fake);

        await view.LoadAsync("ws-1");

        Assert.That(view.Error, Is.Null);
        Assert.That(view.ActivityLabel, Is.EqualTo("Waiting for sign-in"));
    }

    [Test]
    public async Task LoadAsync_keepsAnUnexpectedAuthenticationError()
    {
        var fake = new ChatApiFake
        {
            Session = DesktopDomain.Session()
                .WithAgent("Claude")
                .InState("authentication_required")
                .WithError("spawn failed")
                .IdleWith(DesktopDomain.AvailableAgent("Claude"))
                .Build()
        };
        var view = CreateView(fake);

        await view.LoadAsync("ws-1");

        Assert.That(view.Error, Is.EqualTo("spawn failed"));
    }

    [Test]
    public async Task LoadAsync_labelsAStoppedLiveSessionAsResume()
    {
        var saved = new AgentSessionSummaryDto("s1", "Codex", "Restock featured products", "main", DateTimeOffset.UtcNow);
        var fake = new ChatApiFake
        {
            Session = DesktopDomain.Session()
                .WithAgent("Codex")
                .WithSessionId("s1")
                .InState("stopped")
                .WithSessions(saved)
                .Build()
        };
        var view = CreateView(fake);

        await view.LoadAsync("ws-1");

        Assert.That(view.Sessions.Single().ActionLabel, Is.EqualTo("Resume session →"));
        Assert.That(view.HasNoSavedSessions, Is.False);
    }

    [Test]
    public async Task LoadAsync_notifiesComputedPropertiesAfterClearingSessionContext()
    {
        var notifications = new List<string>();
        var view = CreateView(new ChatApiFake());
        view.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
                notifications.Add(args.PropertyName);
        };

        await view.LoadAsync("ws-1");

        Assert.That(notifications, Does.Contain(nameof(view.HasPermission)));
        Assert.That(notifications, Does.Contain(nameof(view.HasContext)));
        Assert.That(notifications, Does.Contain(nameof(view.HasMessages)));
        Assert.That(view.HasPermission, Is.False);
        Assert.That(view.HasContext, Is.False);
        Assert.That(view.HasMessages, Is.False);
    }

    [Test]
    public async Task SendAsync_reportsCanceledHttpCalls()
    {
        var view = CreateView(new ChatApiFake { SendFailure = new TaskCanceledException("The request was canceled.") });
        await view.LoadAsync("ws-1");
        view.Message = "hello";

        await view.SendCommand.Execute().FirstAsync();

        Assert.That(view.Error, Is.EqualTo("The request was canceled."));
    }

    [Test]
    public async Task LoadAsync_waitsForThePreviousStreamLoopBeforeResetting()
    {
        var hang = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new ChatApiFake { EventsHang = hang };
        var view = CreateView(fake);

        await view.LoadAsync("ws-1");
        var reload = view.LoadAsync("ws-2");
        await reload;

        Assert.That(fake.LastWorkspace, Is.EqualTo("ws-2"));
        Assert.That(view.HasPermission, Is.False);
    }

    [Test]
    public async Task LoadAsync_ignoresErrorsFromSupersededLoads()
    {
        var hang = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new ChatApiFake
        {
            FirstGetHang = hang,
            GetFailures = { ["ws-1"] = new HttpRequestException("stale workspace") }
        };
        var view = CreateView(fake);
        var first = view.LoadAsync("ws-1");
        await view.LoadAsync("ws-2");
        hang.SetResult();
        await first;

        Assert.That(view.Error, Is.Null);
    }

    [Test]
    public async Task LoadAsync_keepsTheAgentCatalogAndHidesAnEmptyTranscript()
    {
        var fake = new ChatApiFake
        {
            Session = DesktopDomain.Session().IdleWith(
                DesktopDomain.AvailableAgent(),
                DesktopDomain.Agent().Named("Cursor").ExecutableMissing().Build()).Build()
        };
        var view = CreateView(fake);

        await view.LoadAsync("ws-1");

        Assert.Multiple(() =>
        {
            Assert.That(view.HasAgent, Is.False);
            Assert.That(view.HasMessages, Is.False);
            Assert.That(view.StatusLine, Is.EqualTo("Idle"));
            Assert.That(view.Agents.Select(agent => agent.DisplayName), Is.EqualTo(["Codex", "Cursor"]));
        });
    }

    [Test]
    public async Task StopAsync_keepsTheAgentCatalogAndClearsTheTranscript()
    {
        var fake = new ChatApiFake
        {
            Session = DesktopDomain.Session().Ready().Build()
        };
        var view = CreateView(fake);
        await view.LoadAsync("ws-1");

        await view.StopCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(view.HasAgent, Is.False);
            Assert.That(view.HasMessages, Is.False);
            Assert.That(view.StatusLine, Is.EqualTo("Idle"));
            Assert.That(view.Agents, Has.Count.EqualTo(1));
            Assert.That(view.Agents[0].DisplayName, Is.EqualTo("Codex"));
        });
    }

    [Test]
    public async Task LoadAsync_hidesThePickerWhenAWorkspaceAgentIsBound()
    {
        var fake = new ChatApiFake
        {
            Session = DesktopDomain.Session()
                .IdleWith(DesktopDomain.AvailableAgent())
                .BoundTo("Codex")
                .WithLoop(new AgentLoopStatusDto(false, "idle", Terminal: "Idle"))
                .Build()
        };
        var view = CreateView(fake);

        await view.LoadAsync("ws-1");

        Assert.Multiple(() =>
        {
            Assert.That(view.HasBoundAgent, Is.True);
            Assert.That(view.CanStartBound, Is.True);
            Assert.That(view.HasAgent, Is.False);
            Assert.That(view.LoopLine, Does.Contain("idle"));
            Assert.That(view.StatusLine, Is.EqualTo("Codex · Idle"));
        });
    }

    [Test]
    public async Task LoadAsync_cannotStartBoundWhileALiveAgentIsSelected()
    {
        var fake = new ChatApiFake
        {
            Session = DesktopDomain.Session()
                .Ready("Codex", "demo-agent")
                .BoundTo("Codex")
                .Build()
        };
        var view = CreateView(fake);

        await view.LoadAsync("ws-1");

        Assert.That(view.CanStartBound, Is.False);
        Assert.That(view.HasAgent, Is.True);
    }

    [Test]
    public async Task LoopLine_includesTheCurrentTaskAndStopReason()
    {
        var fake = new ChatApiFake
        {
            Session = DesktopDomain.Session()
                .Ready()
                .BoundTo("Codex")
                .WithLoop(new AgentLoopStatusDto(
                    true,
                    "blocked",
                    TaskId: "4",
                    TaskNumber: 4,
                    TaskTitle: "Keep this task",
                    Terminal: "BlockedAwaitingInput",
                    StopReason: "termination"))
                .Build()
        };
        var view = CreateView(fake);

        await view.LoadAsync("ws-1");

        Assert.Multiple(() =>
        {
            Assert.That(view.LoopLine, Does.Contain("Keep this task"));
            Assert.That(view.LoopLine, Does.Contain("termination"));
            Assert.That(view.HasLoop, Is.True);
        });
    }

    [Test]
    public async Task LoadAsync_loadsTheAutonomyChecklist()
    {
        var view = CreateView(new ChatApiFake());

        await view.LoadAsync("ws-1");

        Assert.Multiple(() =>
        {
            Assert.That(view.HasAutonomy, Is.True);
            Assert.That(view.Preconditions.Select(item => item.Id), Is.EqualTo(new[]
            {
                "forgeCapabilityEnabled",
                "hostConnectionAuthorized",
                "agentAvailable",
                "autonomyEnabled"
            }));
            Assert.That(view.CanEnableAutonomy, Is.True);
            Assert.That(view.CanActivateLoop, Is.False);
        });
        await view.LoadAsync(null);
    }

    [Test]
    public async Task EnableAutonomyCommand_persistsServerEnablement()
    {
        var fake = new ChatApiFake();
        var view = CreateView(fake);
        await view.LoadAsync("ws-1");

        var sawActivate = false;
        view.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(AgentChatViewModel.CanActivateLoop) && view.CanActivateLoop)
                sawActivate = true;
        };
        await view.EnableAutonomyCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(fake.LastEnabled, Is.True);
            Assert.That(view.CanEnableAutonomy, Is.False);
            Assert.That(view.CanActivateLoop, Is.True);
            Assert.That(sawActivate, Is.True);
        });
        await view.LoadAsync(null);
    }

    [Test]
    public async Task ActivateThenCancel_exposesTheTypedLoopTerminal()
    {
        var fake = new ChatApiFake
        {
            Session = DesktopDomain.Session().Ready().BoundTo("Codex").Build(),
            Autonomy = new(true, [
                new("forgeCapabilityEnabled", true),
                new("hostConnectionAuthorized", true),
                new("agentAvailable", true),
                new("autonomyEnabled", true)
            ])
        };
        var view = CreateView(fake);
        await view.LoadAsync("ws-1");
        await view.ActivateLoopCommand.Execute().FirstAsync();
        await view.CancelLoopCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(view.LoopTerminal, Is.EqualTo("Cancelled"));
            Assert.That(view.HasMessages, Is.False);
            Assert.That(view.CanCancelLoop, Is.False);
        });

        await view.LoadAsync(null);
    }

    [Test]
    public async Task StartBoundCommand_schedulesTheBoundAgent()
    {
        var fake = new ChatApiFake
        {
            Session = DesktopDomain.Session()
                .IdleWith(DesktopDomain.AvailableAgent())
                .BoundTo("Codex")
                .Build(),
            NextSession = DesktopDomain.Session().Ready().BoundTo("Codex").Build()
        };
        var view = CreateView(fake);
        await view.LoadAsync("ws-1");

        await view.StartBoundCommand.Execute().FirstAsync();

        Assert.That(fake.ScheduledAgent, Is.EqualTo("Codex"));
        Assert.That(view.HasAgent, Is.True);
    }

    [Test]
    public void AgentLoopStatusDto_carriesTaskAndTerminalFields()
    {
        var loop = new AgentLoopStatusDto(true, "running", "1", 2, "Ship", "Completed", "end_turn", null, ["write"]);

        Assert.Multiple(() =>
        {
            Assert.That(loop.TaskId, Is.EqualTo("1"));
            Assert.That(loop.TaskNumber, Is.EqualTo(2));
            Assert.That(loop.TaskTitle, Is.EqualTo("Ship"));
            Assert.That(loop.Terminal, Is.EqualTo("Completed"));
        });
    }

    [Test]
    public async Task EmptyFollowUpSnapshot_doesNotClearTheAgentCatalog()
    {
        var fake = new ChatApiFake
        {
            Session = DesktopDomain.Session()
                .IdleWith(DesktopDomain.AvailableAgent())
                .Build(),
            NextSession = DesktopDomain.Session().Ready().WithAgents().Build()
        };
        var view = CreateView(fake);
        await view.LoadAsync("ws-1");

        await view.SelectAgentCommand.Execute("Codex").FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(view.HasAgent, Is.True);
            Assert.That(view.StatusLine, Is.EqualTo("Codex · Idle"));
            Assert.That(view.SelectedAgentDisplayName, Is.EqualTo("Codex"));
            Assert.That(view.Agents, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void ChatItem_usesDisplayRoleWhenPresent()
    {
        Assert.That(new AgentChatItemViewModel("Agent", "Working", displayRole: "Codex").Label, Is.EqualTo("Codex"));
        Assert.That(new AgentChatItemViewModel("You", "Hello").Label, Is.EqualTo("You"));
        Assert.That(new AgentChatItemViewModel("You", "Hello").IsUser, Is.True);
        Assert.That(new AgentChatItemViewModel("You", "Hello").IsWork, Is.False);
    }

    [Test]
    public void Run_thatIsStillLive_showsItsWorkExpandedWithoutAHeader()
    {
        var run = LiveRun();

        Assert.Multiple(() =>
        {
            Assert.That(run.IsLive, Is.True);
            Assert.That(run.ShowHeader, Is.False);
            Assert.That(run.IsExpanded, Is.True);
            Assert.That(run.Chevron, Is.EqualTo("▾"));
        });
    }

    [Test]
    public void Run_summarisesTheWorkItThoughtAndTheToolsItRan()
    {
        var run = LiveRun();

        Assert.Multiple(() =>
        {
            Assert.That(run.WorkItems, Has.Count.EqualTo(2));
            Assert.That(run.Summary, Is.EqualTo("Worked · 1 tool · Thought"));
        });
    }

    [Test]
    public void Run_treatsTheAgentsLastMessageAsItsReply()
    {
        var run = LiveRun();

        Assert.That(run.HasReply, Is.True);
        Assert.That(run.Reply!.Text, Is.EqualTo("Done"));
    }

    // The first seal is the next question arriving: the run collapses behind a header but
    // keeps its reply, because that is what the transcript shows above the new question.
    [Test]
    public void Run_collapsesBehindAHeaderWhenTheNextQuestionSealsIt()
    {
        var run = LiveRun();

        run.Seal();

        Assert.Multiple(() =>
        {
            Assert.That(run.ShowHeader, Is.True);
            Assert.That(run.IsExpanded, Is.False);
            Assert.That(run.Chevron, Is.EqualTo("▸"));
            Assert.That(run.Reply!.Text, Is.EqualTo("Done"));
        });
    }

    [Test]
    public void Run_stopsBeingLiveOnTheSecondSeal()
    {
        var run = LiveRun();
        run.Seal();

        run.Seal();

        Assert.That(run.IsLive, Is.False);
        Assert.That(run.ShowHeader, Is.True);
    }

    /// <summary>A run that thought, ran one tool, and replied - still open.</summary>
    private static AgentRunViewModel LiveRun()
    {
        var run = new AgentRunViewModel();
        run.Items.Add(new AgentChatItemViewModel("Thought", "Looking"));
        run.Items.Add(new AgentChatItemViewModel("Tool", "Search", "completed", "t1"));
        run.Items.Add(new AgentChatItemViewModel("Agent", "Done", displayRole: "Codex"));
        return run;
    }

    [Test]
    public void ChatItem_readsAThoughtAsThoughtRatherThanWork()
    {
        var thought = Thought();

        Assert.Multiple(() =>
        {
            Assert.That(thought.IsThought, Is.True);
            Assert.That(thought.IsWork, Is.False);
            Assert.That(thought.VisibleText, Is.EqualTo("Clarifying test meaning"));
        });
    }

    [Test]
    public void ChatItem_startsAThoughtCollapsed()
    {
        var thought = Thought();

        Assert.That(thought.IsThoughtExpanded, Is.False);
        Assert.That(thought.Label, Is.EqualTo("Thought"));
    }

    // A thought arriving while the agent is still working opens on its own, and is labelled
    // in the present tense for as long as that is true.
    [Test]
    public async Task ChatItem_expandsALiveThoughtAndLetsTheReaderCollapseIt()
    {
        var thought = Thought();

        thought.SetLive(true);

        Assert.That(thought.IsThoughtExpanded, Is.True);
        Assert.That(thought.Label, Is.EqualTo("Thinking"));

        await thought.ToggleCommand.Execute().FirstAsync();

        Assert.That(thought.IsThoughtExpanded, Is.False);
        Assert.That(thought.Label, Is.EqualTo("Thinking"));
    }

    [Test]
    public async Task ChatItem_keepsAFinishedThoughtToggleable()
    {
        var thought = Thought();
        thought.SetLive(true);
        await thought.ToggleCommand.Execute().FirstAsync();

        thought.SetLive(false);
        await thought.ToggleCommand.Execute().FirstAsync();

        Assert.That(thought.IsThoughtExpanded, Is.True);
        Assert.That(thought.Label, Is.EqualTo("Thought"));
    }

    private static AgentChatItemViewModel Thought() =>
        new("Thought", "**Clarifying test meaning**");

    [Test]
    public void AgentDescriptorDto_offersEnableWhenThePackageIsTheGap()
    {
        var agent = DesktopDomain.Agent().Named("claude", "Claude").PackageDisabled().Build();

        Assert.Multiple(() =>
        {
            Assert.That(agent.CanEnable, Is.True);
            Assert.That(agent.IsBlocked, Is.False);
            Assert.That(agent.StatusLabel, Is.EqualTo("Not enabled"));
            Assert.That(agent.HasDetail, Is.True);
            Assert.That(agent.HasRemedy, Is.True);
        });
    }

    [Test]
    public async Task EnableAgentCommand_reportsWhenCapabilitiesAreUnavailable()
    {
        var view = CreateView(new ChatApiFake());
        await view.LoadAsync("ws-1");

        await view.EnableAgentCommand.Execute("claude").FirstAsync();

        Assert.That(view.Error, Does.Contain("cannot enable"));
    }

    [Test]
    public async Task EnableAgentCommand_reloadsTheCatalogAfterEnabling()
    {
        var modules = new RecordingCapabilityModulesApi();
        var fake = new ChatApiFake
        {
            Session = DesktopDomain.Session()
                .IdleWith(DesktopDomain.Agent().Named("claude", "Claude").PackageDisabled().Build())
                .Build()
        };
        var view = CreateView(fake, new CapabilityModulesController(new CapabilityModulesCatalogService(modules)));
        await view.LoadAsync("ws-1");
        fake.Session = DesktopDomain.Session()
            .IdleWith(DesktopDomain.AvailableAgent("claude", "Claude"))
            .Build();

        await view.EnableAgentCommand.Execute("claude").FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(modules.EnabledId, Is.EqualTo("claude"));
            Assert.That(view.Agents.Single().Available, Is.True);
            Assert.That(view.Agents.Single().CanEnable, Is.False);
        });
    }

    [Test]
    public async Task EnableAgentCommand_reportsEnableFailures()
    {
        var modules = new RecordingCapabilityModulesApi
        {
            EnableFailure = new InvalidOperationException("The claude package is not in the registry.")
        };
        var view = CreateView(
            new ChatApiFake(),
            new CapabilityModulesController(new CapabilityModulesCatalogService(modules)));
        await view.LoadAsync("ws-1");

        await view.EnableAgentCommand.Execute("claude").FirstAsync();

        Assert.That(view.Error, Does.Contain("not in the registry"));
    }

    [Test]
    public async Task EnableAgentCommand_doesNothingUntilAWorkspaceIsLoaded()
    {
        var modules = new RecordingCapabilityModulesApi();
        var view = CreateView(
            new ChatApiFake(),
            new CapabilityModulesController(new CapabilityModulesCatalogService(modules)));

        await view.EnableAgentCommand.Execute("claude").FirstAsync();

        Assert.That(modules.EnabledId, Is.Null);
        Assert.That(view.Error, Is.Null);
    }

    [Test]
    public void AgentSessionDto_preservesASubscriptionLoginChallenge()
    {
        var session = DesktopDomain.Session()
            .WithAgent("Cursor")
            .InState("authenticating")
            .IdleWith(DesktopDomain.AvailableAgent("Cursor"))
            .WithAuthMethods(new AgentAuthMethodDto("cursor_login", "Cursor Login", "Sign in"))
            .WithLoginChallenge(new AgentLoginChallengeDto("https://cursor.com/loginDeepControl?challenge=abc", "ABCD-EFGHI", "Open this link"))
            .Build();

        Assert.Multiple(() =>
        {
            Assert.That(session.WorkspaceId, Is.EqualTo("ws-1"));
            Assert.That(session.LoginChallenge!.Url, Does.Contain("loginDeepControl"));
            Assert.That(session.LoginChallenge.Code, Is.EqualTo("ABCD-EFGHI"));
            Assert.That(session.LoginChallenge.RedirectUri, Is.Null);
        });
    }

    [Test]
    public void AgentLoginChallengeDto_carriesARedirectCallback()
    {
        var challenge = new AgentLoginChallengeDto(
            "https://auth.openai.com/oauth/authorize",
            null,
            "Open this link",
            AgentLoginTransport.Redirect,
            RedirectUri: "http://localhost:1455/auth/callback");

        Assert.That(challenge.Transport, Is.EqualTo(AgentLoginTransport.Redirect));
        Assert.That(challenge.RedirectUri, Is.EqualTo("http://localhost:1455/auth/callback"));
    }

    [Test]
    public void AgentLoginChallengeDto_deserializesWithoutOptionalExpiry()
    {
        var challenge = System.Text.Json.JsonSerializer.Deserialize<AgentLoginChallengeDto>(
            """{"url":"https://example.test/login","instructions":"Open this link"}""",
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(challenge!.ExpiresAt, Is.Null);
        Assert.That(challenge.RedirectUri, Is.Null);
    }

    private static AgentChatViewModel CreateView(IAgentApiProvider provider, CapabilityModulesController? modules = null)
        => new(new AgentsController(new AgentChatService(provider)), modules);
}

internal sealed class ChatApiFake : IAgentApiProvider
{
    public Exception? SendFailure { get; set; }
    public TaskCompletionSource? EventsHang { get; set; }
    public TaskCompletionSource? FirstGetHang { get; set; }
    public Dictionary<string, Exception> GetFailures { get; } = new(StringComparer.Ordinal);
    public string? LastWorkspace { get; private set; }
    public string? ResumedSessionId { get; private set; }
    public string? SubmittedCode { get; private set; }
    public string? ScheduledAgent { get; private set; }
    private int _gets;

    public AgentSessionDto? Session { get; set; }
    public AgentSessionDto? NextSession { get; set; }

    public async Task<AgentSessionDto?> GetAsync(string workspaceId, CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _gets) == 1 && FirstGetHang is not null)
            await FirstGetHang.Task;
        LastWorkspace = workspaceId;
        if (GetFailures.TryGetValue(workspaceId, out var failure))
            throw failure;
        return Session ?? DesktopDomain.Session().ForWorkspace(workspaceId).Build();
    }

    public Task<AgentSessionDto?> BindAsync(string workspaceId, string agent, CancellationToken cancellationToken)
        => Task.FromResult(NextSession ?? Session);

    public Task<AgentSessionDto?> ScheduleAsync(string workspaceId, string agent, CancellationToken cancellationToken)
    {
        ScheduledAgent = agent;
        return Task.FromResult(NextSession ?? Session);
    }

    public Task<AgentSessionDto?> ResumeAsync(string workspaceId, string sessionId, CancellationToken cancellationToken)
    {
        ResumedSessionId = sessionId;
        return Task.FromResult(NextSession ?? Session);
    }

    public Task SendAsync(string workspaceId, string message, CancellationToken cancellationToken)
        => SendFailure is null ? Task.CompletedTask : Task.FromException(SendFailure);

    public Task AuthenticateAsync(string workspaceId, string methodId, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task SubmitLoginCodeAsync(string workspaceId, string code, CancellationToken cancellationToken)
    {
        SubmittedCode = code;
        return Task.CompletedTask;
    }
    public Task DecideAsync(string workspaceId, string requestId, string optionId, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(string workspaceId, CancellationToken cancellationToken) => Task.CompletedTask;

    public ServerAutonomyDto? Autonomy { get; set; } = new(false, [
        new("forgeCapabilityEnabled", true),
        new("hostConnectionAuthorized", true),
        new("agentAvailable", true),
        new("autonomyEnabled", false, "Enable autonomy for this Server")
    ]);
    public bool? LastEnabled { get; private set; }
    public bool? LastActive { get; private set; }

    public Task<ServerAutonomyDto?> GetAutonomyAsync(CancellationToken cancellationToken)
        => Task.FromResult(Autonomy);

    public Task PutAutonomyAsync(bool enabled, CancellationToken cancellationToken)
    {
        LastEnabled = enabled;
        Autonomy = new(enabled, (Autonomy?.Preconditions ?? []).Select(item =>
            item.Id == "autonomyEnabled"
                ? item with { Satisfied = enabled, Remedy = enabled ? null : "Enable autonomy for this Server" }
                : item).ToArray());
        return Task.CompletedTask;
    }

    public Task PutLoopActivationAsync(string workspaceId, bool active, CancellationToken cancellationToken)
    {
        LastActive = active;
        LastWorkspace = workspaceId;
        if (Session is not null)
        {
            Session = Session with
            {
                Loop = active
                    ? new AgentLoopStatusDto(true, "running", TaskTitle: "Restock featured products")
                    : new AgentLoopStatusDto(false, "stopped", Terminal: "Cancelled", StopReason: "cancelled")
            };
        }
        return Task.CompletedTask;
    }

    public Task<AgentLoopStatusDto?> GetLoopAsync(string workspaceId, CancellationToken cancellationToken)
        => Task.FromResult(Session?.Loop);

    public async IAsyncEnumerable<AgentEventDto> EventsAsync(
        string workspaceId,
        long after,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (EventsHang is not null)
            await EventsHang.Task.WaitAsync(cancellationToken);
        yield break;
    }
}

internal sealed class RecordingCapabilityModulesApi : ICapabilityModulesApiProvider
{
    public string? EnabledId { get; private set; }
    public Exception? EnableFailure { get; set; }

    public Task<IReadOnlyList<CapabilityModuleDto>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CapabilityModuleDto>>([]);

    public Task<CapabilityModuleDto> EnableAsync(string id, string? version, CancellationToken cancellationToken = default)
    {
        if (EnableFailure is not null)
            return Task.FromException<CapabilityModuleDto>(EnableFailure);
        EnabledId = id;
        return Task.FromResult(new CapabilityModuleDto(id, "1.0.0", id, "agent-up", "agent", true, "ready", true, []));
    }

    public Task<CapabilityModuleDto> DisableAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(new CapabilityModuleDto(id, "1.0.0", id, "agent-up", "agent", false, "disabled", false, []));
}
