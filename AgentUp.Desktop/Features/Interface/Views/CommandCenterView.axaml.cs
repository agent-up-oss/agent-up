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
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Services;
using AgentUp.Desktop.Features.Interface.ViewModels;
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
    private string? _applied;
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
        Watch(center.Tasks?.Queue);
        Watch(center.Tasks?.Working);
        Watch(center.Tasks?.Offered);
        Watch(main.Validation?.Flows);
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
        PlaceSceneSurfaces(center);
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
            main.Login.CurrentServerUrl,
            center.Agent?.WorkspaceId,
            _validationsOpen,
            running is not null,
            passed,
            summary,
            team,
            TasksOf(center.Tasks),
            FlowsOf(main.Validation)));
        var stamp = Stamp(center.SceneId, binding);
        if (stamp == _applied) return;
        _applied = stamp;
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

    private void PlaceSceneSurfaces(CommandCenterViewModel center)
    {
        RestoreSceneSurfaces();
        if (!center.Active) return;
        var window = TopLevel.GetTopLevel(this);
        if (window is null) return;
        switch (center.SceneId)
        {
            case "application":
                Take("application", window.FindControl<Control>("PortPane"));
                break;
            case "agent-git":
                Take("changes", window.FindControl<Control>("GitPanel"));
                break;
            case "agent-chat":
                Take("transcript", window.FindControl<Control>("AgentChatPane"));
                break;
            case "agent-overview":
                Take("overview", window.FindControl<Control>("OverviewContent"));
                break;
        }
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
        foreach (var slot in new[] { "application", "changes", "transcript", "overview" })
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
            case "nav-settings":
                center.ShowNav(2);
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
            case "tab-application":
                center.ShowSection("application");
                break;
            case "back":
                center.CloseAgentCommand.Execute().Subscribe();
                break;
            case "add-workspace":
                main.Sidebar.ShowAddWorkspaceCommand.Execute().Subscribe();
                break;
            case "logout":
                main.ResetLocalSession();
                break;
            case "use-classic":
                center.ToggleCommand.Execute().Subscribe();
                break;
            case "open-workspace":
                Open(center, action.Id);
                break;
            case "connect-source":
                center.TaskSource?.ConnectCommand.Execute().Subscribe();
                break;
            case "reload":
                main.Git.RefreshCommand.Execute().Subscribe();
                break;
            case "open-validations":
                _validationsOpen = !_validationsOpen;
                Apply();
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
                if (int.TryParse(action.Id, out var index) && center.Tasks is { } tasks && index >= 0 && index < tasks.Offered.Count)
                    tasks.Offered[index].PickCommand.Execute().Subscribe();
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

    private static void Open(CommandCenterViewModel center, string? id)
    {
        var standing = center.Bridge?.Team.FirstOrDefault(row => row.WorkspaceId == id)?.Standing;
        if (standing is not null) center.OpenAgentCommand.Execute(standing).Subscribe();
    }

    private void Run(MainViewModel main, string? id)
    {
        var flow = main.Validation?.Flows.FirstOrDefault(item => item.Flow.Id == id);
        if (flow is null) return;
        _runningFlowId = flow.Flow.Id;
        _validationsOpen = false;
        flow.RunCommand.Execute().Subscribe(_ => Apply());
        Apply();
    }

    private static IReadOnlyList<CommandCenterTaskSlot> TasksOf(TaskQueueViewModel? tasks)
    {
        if (tasks is null) return [];
        return
        [
            .. tasks.Queue.Select(row => new CommandCenterTaskSlot(row.Id, row.TaskLabel, row.RepositoryLabel, "", "dequeue", "Dequeue")),
            .. tasks.Working.Select(row => new CommandCenterTaskSlot(row.Id, row.TaskLabel, row.RepositoryLabel, "", "stop", "Stop")),
            .. tasks.Offered.Select((row, index) => new CommandCenterTaskSlot(index.ToString(), row.IssueLabel, "", "", "queue", "Queue")),
        ];
    }

    private static IReadOnlyList<CommandCenterFlowSlot> FlowsOf(ValidationViewModel? validation)
        => validation?.Flows.Select(flow => new CommandCenterFlowSlot(flow.Flow.Id, flow.Name, flow.Description)).ToArray()
           ?? [];
}
