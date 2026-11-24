using System.Collections.Specialized;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AgentUp.Desktop.DesignSystem;
using AgentUp.Desktop.Features.Agents.ViewModels;
using AgentUp.Desktop.Features.Applications.ViewModels;
using AgentUp.Desktop.Features.Interface.Services;
using AgentUp.Desktop.Features.Interface.ViewModels;
using AgentUp.Desktop.Features.Ports.ViewModels;
using AgentUp.Desktop.Features.Validation.Models;
using AgentUp.Desktop.Features.Validation.ViewModels;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Interface.Views;

public partial class CommandCenterView : UserControl
{
    private readonly CompositeDisposable _subscriptions = new();
    private readonly Dictionary<Control, (Panel Parent, int Index)> _homes = new();
    private bool _validationsOpen;
    private string? _runningFlowId;
    private readonly CommandCenterTaskSourceBoard _board = new();
    private string? _applied;
    private string? _heldScene;
    private string? _heldChrome;
    private bool _applyQueued;

    public CommandCenterView()
    {
        InitializeComponent();
        Scene.ActionInvoked += OnAction;
        // Bubble, after the control under the pointer has handled the press. A tunnel handler
        // started a window drag before that control saw the click, so the click never landed.
        AddHandler(PointerPressedEvent, OnChromePressed, RoutingStrategies.Bubble);
        DataContextChanged += (_, _) => Rewire();
    }

    public void Host(string slot, Control control) => Scene.SetContent(slot, control);

    public Control? Release(string slot) => Scene.DetachContent(slot);

    private void Rewire()
    {
        _subscriptions.Clear();
        if (DataContext is not MainViewModel main || main.CommandCenter is not { } center) return;
        center.WhenAnyValue(item => item.SceneId, item => item.Active)
            .Subscribe(_ => Apply())
            .DisposeWith(_subscriptions);
        if (center.Bridge is { } bridge)
        {
            void OnBridgeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => Apply();
            bridge.PropertyChanged += OnBridgeChanged;
            Disposable.Create(() => bridge.PropertyChanged -= OnBridgeChanged).DisposeWith(_subscriptions);
        }
        main.Login.WhenAnyValue(login => login.IsVisible).Subscribe(_ => Apply()).DisposeWith(_subscriptions);
        main.WhenAnyValue(item => item.AddressBarUrl).Subscribe(_ => Apply()).DisposeWith(_subscriptions);
        main.WhenAnyValue(item => item.SelectedSubTab).Subscribe(_ => Apply()).DisposeWith(_subscriptions);
        main.Applications.WhenAnyValue(item => item.SelectedApplication).Subscribe(_ => Apply()).DisposeWith(_subscriptions);
        main.Sidebar.WhenAnyValue(item => item.SelectedWorkspace).Subscribe(_ => Apply()).DisposeWith(_subscriptions);
        if (main.Validation is { } validation)
            validation.WhenAnyValue(item => item.IsCollapsed).Subscribe(_ => Apply()).DisposeWith(_subscriptions);
        // A file opened from Files is its own page; Back returns to the tree. The panel owns that
        // choice, so the scene follows it rather than the other way round.
        main.Files.WhenAnyValue(files => files.IsEditorOpen)
            .Subscribe(open =>
            {
                if (center.SceneId is "agent-files" or "agent-file")
                    center.ShowSection(open ? "file" : "files");
            })
            .DisposeWith(_subscriptions);
        WatchAgent(main.Agent);
        Watch(main.SubTabs);
        Watch(center.Tasks?.Queue);
        Watch(center.Tasks?.Working);
        Watch(center.Tasks?.Offered);
        Watch(main.Validation?.Flows);
        Watch(main.Modules?.Modules);
        Watch(main.Applications.Applications);
        Apply();
    }

    private void Watch(INotifyCollectionChanged? collection)
    {
        if (collection is null) return;
        NotifyCollectionChangedEventHandler handler = (_, _) => Apply();
        collection.CollectionChanged += handler;
        Disposable.Create(() => collection.CollectionChanged -= handler).DisposeWith(_subscriptions);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Apply();
    }

    private void Apply()
    {
        if (_applyQueued) return;
        _applyQueued = true;
        Dispatcher.UIThread.Post(Pump, DispatcherPriority.Background);
    }

    private void Pump()
    {
        _applyQueued = false;
        ApplyNow();
    }

    private void ApplyNow()
    {
        if (!IsEffectivelyVisible)
        {
            _applied = null;
            return;
        }
        if (DataContext is not MainViewModel main || main.CommandCenter is not { } center) return;
        _ = LoadSource(center);
        var tool = ToolOf(main.SelectedSubTab);
        var browser = tool is "port" or "desktop";
        PlaceChatSettings(center);
        var swapped = PlaceSceneSurfaces(center, main);
        var team = center.Bridge?.Team.Select(row => row.Standing).ToArray() ?? [];
        var running = main.Validation?.Flows.FirstOrDefault(flow => flow.IsRunning);
        var passed = running is null && _runningFlowId is not null
            && main.Validation?.Flows.FirstOrDefault(flow => flow.Flow.Id == _runningFlowId)?.RunState
                == ValidationRunState.Passed;
        var summary = running?.Name
            ?? main.Validation?.Flows.FirstOrDefault(flow => flow.Flow.Id == _runningFlowId)?.Name
            ?? "No check selected";
        var binding = CommandCenterScene.Bind(new CommandCenterSceneInput(
            center.SceneId,
            main.Login.ConnectedServerName,
            center.Agent?.WorkspaceId,
            _validationsOpen,
            running is not null,
            passed,
            summary,
            team,
            _board.TasksOf(center.Tasks, team),
            FlowsOf(main.Validation),
            main.AddressBarUrl ?? "",
            main.SubTabs.OfType<PortSubTabViewModel>().FirstOrDefault(tab => tab.IsHttp)?.Label ?? "",
            tool,
            browser,
            ToolsOf(main),
            main.Validation?.IsCollapsed != false,
            main.Sidebar.SelectedWorkspace?.Id ?? center.Agent?.WorkspaceId,
            ChatSettingsOf(main.Agent),
            main.Applications.SelectedApplication?.Name,
            main.Modules?.Modules.Select(module => new CommandCenterModuleSlot(module.Id, module.DisplayName, module.Enabled, module.CanRun, module.Available ? module.StatusLabel : module.EnableLabel, module.Available)).ToArray(),
            main.Applications.Applications.Select(application => new CommandCenterApplicationSlot(
                application.Name,
                string.Equals(application.State, "Running", StringComparison.OrdinalIgnoreCase))).ToArray(),
            _board.VisibleDevPulls,
            _board.VisiblePmPulls,
            _board.TasksTab,
            _board.RepositorySlots(),
            _board.OpenRepositoryLabel,
            _board.ForgeTab,
            center.TaskSource?.DetailFor(_board.ForgeTab) ?? "",
            center.TaskSource?.EndpointFor(_board.ForgeTab) ?? CommandCenterScene.PrefillEndpoint(_board.ForgeTab, null),
            center.TaskSource?.IsConnected(_board.ForgeTab) == true));
        binding.State["chatSettings"] = "1";
        var stamp = Stamp(center.SceneId, binding);
        if (stamp == _applied) return;
        var app = binding.State.TryGetValue("app", out var selectedApp) ? selectedApp : "";
        var chrome = tool + "|" + browser + "|" + (running is not null) + "|" + binding.State.ContainsKey("checks") + "|" + binding.State.ContainsKey("collapsed") + "|" + app;
        if (!swapped && _heldScene == center.SceneId && _heldChrome == chrome && HostsNativeView())
        {
            _applied = stamp;
            ShowAddress(binding.Text);
            return;
        }
        _applied = stamp;
        _heldScene = center.SceneId;
        _heldChrome = chrome;
        Scene.Apply(center.SceneId, binding.Text, binding.Items, binding.State);
    }

    private static string Stamp(string scene, CommandCenterSceneBinding binding)
    {
        var text = string.Join('\n', binding.Text.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value));
        var state = string.Join('\n', binding.State.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value));
        return scene + "\n" + text + "\n" + state + "\n" + StampItems(binding.Items);
    }

    private static string StampItems<T>(IEnumerable<KeyValuePair<string, T>> items) where T : IEnumerable<NewUiSlotItem>
        => string.Join('\n', items.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + ":" + string.Join(",", pair.Value.Select(StampItem))));

    private static string StampItem(NewUiSlotItem item)
        => item.Id + "|" + item.Action + "|" + item.State + "|"
           + string.Join(",", item.Parts.OrderBy(part => part, StringComparer.Ordinal)) + "|"
           + string.Join(",", item.Text.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value)) + "|"
           + StampItems(item.Items);

    private bool PlaceSceneSurfaces(CommandCenterViewModel center, MainViewModel main)
    {
        if (TopLevel.GetTopLevel(this) is not TopLevel window) return false;
        if (!center.Active)
        {
            RestoreSceneSurfaces();
            return false;
        }
        var (slot, control) = Surface(center.SceneId, window, main);
        if (control is not null && Inside(Scene, control)) return false;
        RestoreSceneSurfaces();
        if (control is null) return false;
        control.ClipToBounds = true;
        Take(slot, control);
        return true;
    }

    private static (string Slot, Control? Control) Surface(string sceneId, TopLevel window, MainViewModel main) => sceneId switch
    {
        "application" => ("application", ApplicationSurface(window, main)),
        "agent-git" => ("changes", window.FindControl<Control>("GitPanel")),
        "agent-files" or "agent-file" => ("files", window.FindControl<Control>("FilesPanel")),
        "agent-chat" => ("transcript", window.FindControl<Control>("AgentChatPane")),
        "agent-overview" => ("overview", window.FindControl<Control>("OverviewContent")),
        _ => ("", null),
    };

    private static Control? ApplicationSurface(TopLevel window, MainViewModel main) => main.SelectedSubTab switch
    {
        ConsoleSubTabViewModel => window.FindControl<Control>("ConsoleHost"),
        AuditSubTabViewModel => window.FindControl<Control>("AuditPanel"),
        MetricsSubTabViewModel => window.FindControl<Control>("MetricsPanel"),
        DatabaseSubTabViewModel => window.FindControl<Control>("DatabasePanel"),
        PortSubTabViewModel { IsHttp: false } => window.FindControl<Control>("TcpInfo"),
        _ => window.FindControl<Control>("PortPane"),
    };

    private static string ToolOf(SubTabViewModel? tab) => tab switch
    {
        ConsoleSubTabViewModel => "console",
        AuditSubTabViewModel => "diagnostics",
        MetricsSubTabViewModel => "metrics",
        DatabaseSubTabViewModel => "database",
        PortSubTabViewModel { IsHttp: false } => "tcp",
        _ => "port",
    };

    private static string[] ToolsOf(MainViewModel main)
    {
        var tools = new List<string>();
        if (main.SubTabs.Any(tab => tab is PortSubTabViewModel { IsHttp: true } or DesktopSubTabViewModel))
            tools.Add("port");
        if (main.SubTabs.OfType<ConsoleSubTabViewModel>().Any()) tools.Add("console");
        if (main.SubTabs.OfType<AuditSubTabViewModel>().Any()) tools.Add("diagnostics");
        if (main.SubTabs.OfType<MetricsSubTabViewModel>().Any()) tools.Add("metrics");
        if (main.SubTabs.OfType<DatabaseSubTabViewModel>().Any()) tools.Add("database");
        return tools.ToArray();
    }

    private static bool Inside(Control root, Control control)
    {
        for (StyledElement? node = control; node is not null; node = node.Parent)
        {
            if (ReferenceEquals(node, root)) return true;
        }
        return false;
    }

    private bool HostsNativeView()
        => Scene.GetVisualDescendants().OfType<NativeWebView>().Any(view => view.IsEffectivelyVisible);

    private void ShowAddress(IReadOnlyDictionary<string, string> text)
    {
        if (!text.TryGetValue("address", out var url)) return;
        var box = Scene.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(item => item.Classes.Contains("au-address-bar"));
        if (box is null || box.IsFocused || box.Text == url) return;
        box.Text = url;
    }

    private void PlaceChatSettings(CommandCenterViewModel center)
    {
        if (TopLevel.GetTopLevel(this) is not TopLevel window) return;
        var settings = window.FindControl<Control>("ChatSettings");
        if (settings is null) return;
        if (Inside(Scene, settings)) Scene.DetachContent("chat-settings");
        if (!center.Active)
        {
            RestoreHome(settings);
            return;
        }
        if (settings.Parent is not null) Park(settings);
    }

    private void Park(Control control)
    {
        if (control.Parent is Panel panel && !_homes.ContainsKey(control))
            _homes[control] = (panel, panel.Children.IndexOf(control));
        if (control.Parent is Panel parent) parent.Children.Remove(control);
    }

    private void RestoreHome(Control control)
    {
        if (!_homes.TryGetValue(control, out var home) || control.Parent is not null) return;
        home.Parent.Children.Insert(Math.Min(home.Index, home.Parent.Children.Count), control);
    }

    private void Take(string slot, Control? control)
    {
        if (control is null) return;
        if (control.Parent is Panel panel && !_homes.ContainsKey(control))
            _homes[control] = (panel, panel.Children.IndexOf(control));
        Host(slot, control);
    }

    private void RestoreSceneSurfaces()
    {
        foreach (var slot in new[] { "application", "changes", "files", "transcript", "overview" })
        {
            var control = Release(slot);
            if (control is null || !_homes.TryGetValue(control, out var home)) continue;
            if (control.Parent is not null) continue;
            home.Parent.Children.Insert(Math.Min(home.Index, home.Parent.Children.Count), control);
        }
    }

    private void OnAction(object? sender, NewUiActionEventArgs action)
    {
        if (DataContext is not MainViewModel main || main.CommandCenter is not { } center) return;
        switch (action.Name)
        {
            case "nav-team":
                center.ShowNav(0);
                break;
            case "nav-tasks":
                center.ShowNav(1);
                break;
            case "tasks-issues":
                ShowTasksTab("issues");
                break;
            case "tasks-dev-review":
                ShowTasksTab("dev");
                break;
            case "tasks-pm-review":
                ShowTasksTab("pm");
                break;
            case "nav-settings":
                center.ShowNav(2);
                if (main.Modules is { } listed) _ = listed.RefreshAsync();
                break;
            case "enable-module":
                main.Modules?.Modules.FirstOrDefault(module => module.Id == action.Id)?.EnableCommand.Execute().Subscribe();
                break;
            case "disable-module":
                main.Modules?.Modules.FirstOrDefault(module => module.Id == action.Id)?.DisableCommand.Execute().Subscribe();
                break;
            case "tab-overview":
                center.ShowSection("overview");
                break;
            case "tab-chat":
                center.ShowSection("chat");
                break;
            case "tab-git":
                center.ShowSection("git");
                break;
            case "tab-files":
                center.ShowSection(main.Files.IsEditorOpen ? "file" : "files");
                break;
            case "tab-application":
                center.ShowSection("application");
                break;
            case "select-application":
                center.ShowSection("application");
                var application = main.Applications.Applications.FirstOrDefault(item => item.Name == action.Id);
                if (application is not null) main.Applications.SelectedApplication = application;
                break;
            case "subtab-port":
                SelectSubTab(main, tab => tab is PortSubTabViewModel { IsHttp: true } or DesktopSubTabViewModel);
                break;
            case "subtab-console":
                SelectSubTab(main, tab => tab is ConsoleSubTabViewModel);
                break;
            case "subtab-diagnostics":
                SelectSubTab(main, tab => tab is AuditSubTabViewModel);
                break;
            case "subtab-metrics":
                SelectSubTab(main, tab => tab is MetricsSubTabViewModel);
                break;
            case "subtab-database":
                SelectSubTab(main, tab => tab is DatabaseSubTabViewModel);
                break;
            case "back":
                center.CloseAgentCommand.Execute().Subscribe();
                break;
            case "add-workspace":
                main.Sidebar.ShowAddWorkspaceCommand.Execute().Subscribe();
                break;
            case "logout":
                main.LogoutCommand.Execute().Subscribe();
                break;
            case "use-classic":
                center.ToggleCommand.Execute().Subscribe();
                break;
            case "open-workspace":
                Open(center, action.Id);
                break;
            case "forge-tab":
                _board.ShowForgeTab(action.Id);
                Apply();
                break;
            case "save-token":
                _ = SaveTokenAsync(center, action);
                break;
            case "forget-token":
                _ = ForgetTokenAsync(center);
                break;
            case "open-repo":
                OpenRepository(center, action.Id);
                break;
            case "tasks-back":
                _board.CloseRepository(center.Tasks);
                if (center.Tasks is { } tasks)
                    _ = tasks.ReadAsync();
                Apply();
                break;
            case "reload":
                main.Git.RefreshCommand.Execute().Subscribe();
                break;
            case "refresh":
                main.Sidebar.RefreshCommand.Execute().Subscribe();
                break;
            case "browser-back":
                main.BrowserBackCommand.Execute().Subscribe();
                break;
            case "browser-forward":
                main.BrowserForwardCommand.Execute().Subscribe();
                break;
            case "browser-reload":
                main.BrowserReloadCommand.Execute().Subscribe();
                break;
            case "navigate-address":
                if (string.IsNullOrWhiteSpace(action.Text)) break;
                main.AddressBarUrl = action.Text;
                main.NavigateAddressCommand.Execute().Subscribe();
                break;
            case "open-validations":
                _validationsOpen = !_validationsOpen;
                Apply();
                break;
            case "toggle-validation":
                main.Validation?.ToggleCommand.Execute().Subscribe();
                Apply();
                break;
            case "settings-workspace":
                var workspace = main.Sidebar.Workspaces.FirstOrDefault(item => item.Id == action.Id);
                if (workspace is not null) main.Sidebar.SelectedWorkspace = workspace;
                break;
            case "enable-autonomy":
                main.Agent.EnableAutonomyCommand.Execute().Subscribe();
                break;
            case "activate-loop":
                main.Agent.ActivateLoopCommand.Execute().Subscribe();
                break;
            case "cancel-loop":
                main.Agent.CancelLoopCommand.Execute().Subscribe();
                break;
            case "start-bound":
                main.Agent.StartBoundCommand.Execute().Subscribe();
                break;
            case "select-agent":
                if (!string.IsNullOrEmpty(action.Id)) main.Agent.SelectAgentCommand.Execute(action.Id).Subscribe();
                break;
            case "enable-agent":
                if (!string.IsNullOrEmpty(action.Id)) main.Agent.EnableAgentCommand.Execute(action.Id).Subscribe();
                break;
            case "run":
                Run(main, action.Id);
                break;
            case "dequeue":
                center.Tasks?.Queue.FirstOrDefault(row => row.Id == action.Id)?.RemoveCommand.Execute().Subscribe();
                break;
            case "stop":
                center.Tasks?.Working.FirstOrDefault(row => row.Id == action.Id)?.StopCommand.Execute().Subscribe();
                break;
            case "queue":
                _board.PickOffered(center.Tasks, action.Id);
                break;
            case "window-minimize":
                if (TopLevel.GetTopLevel(this) is Window minimize) minimize.WindowState = WindowState.Minimized;
                break;
            case "window-restore":
                if (TopLevel.GetTopLevel(this) is Window restore)
                    restore.WindowState = restore.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                break;
            case "window-close":
                if (TopLevel.GetTopLevel(this) is Window close) close.Close();
                break;
        }
    }

    private void OnChromePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.Source is not Visual source || !InsideChrome(source) || source.GetVisualAncestors().OfType<Button>().Any() || source is Button) return;
        if (TopLevel.GetTopLevel(this) is not Window window) return;
        if (e.ClickCount >= 2)
            window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            window.BeginMoveDrag(e);
    }

    private static bool InsideChrome(Visual source)
    {
        for (Visual? current = source; current is not null; current = current.GetVisualParent())
        {
            if (current is Control control && control.Classes.Contains("au-chrome")) return true;
        }
        return false;
    }

    private static void SelectSubTab(MainViewModel main, Func<SubTabViewModel, bool> match)
    {
        var tab = main.SubTabs.FirstOrDefault(match);
        if (tab is not null) main.SelectedSubTab = tab;
    }

    private static void Open(CommandCenterViewModel center, string? id)
    {
        var standing = center.Bridge?.Team.FirstOrDefault(row => row.WorkspaceId == id)?.Standing;
        if (standing is not null) center.OpenAgentCommand.Execute(standing).Subscribe();
    }

    private void Run(MainViewModel main, string? id)
    {
        var flow = main.Validation?.Flows.FirstOrDefault(item => item.Flow.Id == id);
        if (flow is null) return;
        // Two replays on one page interleave their clicks and both fail; the running one finishes first.
        if (main.Validation!.Flows.Any(item => item.IsRunning)) return;
        _runningFlowId = flow.Flow.Id;
        _validationsOpen = false;
        flow.RunCommand.Execute().Subscribe(_ => Apply());
        Apply();
    }

    private void ShowTasksTab(string tab)
    {
        _board.ShowTasksTab(tab);
        Apply();
    }

    private async Task LoadSource(CommandCenterViewModel center)
    {
        if (_board.SourceRequested || center.TaskSource is null)
            return;
        await _board.EnsureSourceAsync(center.TaskSource);
        Apply();
    }

    private async Task SaveTokenAsync(CommandCenterViewModel center, NewUiActionEventArgs action)
    {
        await _board.SaveTokenAsync(center.TaskSource, action.Fields, action.Text);
        Apply();
    }

    private async Task ForgetTokenAsync(CommandCenterViewModel center)
    {
        await _board.ForgetTokenAsync(center.TaskSource);
        Apply();
    }

    private void OpenRepository(CommandCenterViewModel center, string? id)
    {
        if (!_board.OpenRepository(id, center.Tasks))
            return;
        _ = LoadOpenRepository(center);
    }

    private async Task LoadOpenRepository(CommandCenterViewModel center)
    {
        await _board.LoadOpenRepositoryAsync(center.Tasks, center.TaskSource);
        Apply();
    }

    private void WatchAgent(AgentChatViewModel agent)
    {
        Watch(agent.Preconditions);
        Watch(agent.Agents);
        void OnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName is nameof(AgentChatViewModel.BoundAgent)
                or nameof(AgentChatViewModel.Loop)
                or nameof(AgentChatViewModel.IsBusy)
                or nameof(AgentChatViewModel.CanEnableAutonomy)
                or nameof(AgentChatViewModel.CanActivateLoop)
                or nameof(AgentChatViewModel.CanCancelLoop)
                or nameof(AgentChatViewModel.CanStartBound))
                Apply();
        }
        agent.PropertyChanged += OnChanged;
        Disposable.Create(() => agent.PropertyChanged -= OnChanged).DisposeWith(_subscriptions);
    }

    private static IReadOnlyList<CommandCenterSettingSlot> ChatSettingsOf(AgentChatViewModel agent)
        => CommandCenterScene.ChatFields(new CommandCenterChatSettings(
            agent.Preconditions.ToArray(),
            agent.Agents.ToArray(),
            agent.BoundAgent,
            agent.LoopLine,
            agent.CanEnableAutonomy,
            agent.CanActivateLoop,
            agent.CanCancelLoop,
            agent.CanStartBound));

    private static IReadOnlyList<CommandCenterFlowSlot> FlowsOf(ValidationViewModel? validation)
        => validation?.Flows.Select(flow => new CommandCenterFlowSlot(
            flow.Flow.Id,
            flow.Name,
            flow.Description,
            Tone(flow.RunState),
            flow.Stages.Select(stage => new CommandCenterStageSlot(
                stage.Id,
                stage.Title,
                Tone(stage.State),
                stage.Checks.Select(check => new CommandCenterCheckSlot(check.Label, Tone(check.State))).ToArray())).ToArray())).ToArray()
           ?? [];

    private static string Tone(ValidationRunState state) => state switch
    {
        ValidationRunState.Passed => "passed",
        ValidationRunState.Failed => "failed",
        ValidationRunState.Running => "running",
        _ => "pending",
    };
}
