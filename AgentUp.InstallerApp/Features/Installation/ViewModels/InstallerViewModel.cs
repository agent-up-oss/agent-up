using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AgentUp.InstallerApp.Features.Capabilities.Models;
using AgentUp.InstallerApp.Features.Capabilities.Services;
using AgentUp.InstallerApp.Features.Logging;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.InstallerApp.Features.Installation.ViewModels;

public sealed class InstallerViewModel : INotifyPropertyChanged
{
    private readonly IInstallerPlatformAdapter _adapter;
    private readonly CapabilityDashboardService _capabilities;
    private readonly InstallerSession _session;
    private string _page = "Dashboard";
    private CapabilityCardViewModel? _selectedCapability;
    private bool _isCapabilityEditVisible;

    public InstallerViewModel(
        InstallerSession session,
        IInstallerPlatformAdapter adapter,
        CapabilityDashboardService capabilities)
    {
        _session = session;
        _adapter = adapter;
        _capabilities = capabilities;
        ComponentCards = new ObservableCollection<ComponentCardViewModel>(
            session.Manifest.Components.Select(c =>
                new ComponentCardViewModel(c, c.DisplayName, c.Description, this, adapter.SupportsInstallActions)));
        AddModuleCommand = new DelegateCommand(async _ => await ShowAddModuleAsync());
        BackToDashboardCommand = new DelegateCommand(_ => ShowDashboard());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ComponentCardViewModel> ComponentCards { get; }

    public ObservableCollection<CapabilityCardViewModel> CapabilityCards { get; } = [];

    public ObservableCollection<CatalogCapabilityViewModel> CatalogEntries { get; } = [];

    public ICommand AddModuleCommand { get; }

    public ICommand BackToDashboardCommand { get; }

    internal bool SupportsCapabilityInstallActions => _capabilities.SupportsInstallActions;

    public string Page
    {
        get => _page;
        private set
        {
            if (_page == value)
                return;
            _page = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDashboardVisible));
            OnPropertyChanged(nameof(IsAddModuleVisible));
        }
    }

    public bool IsDashboardVisible => Page == "Dashboard";

    public bool IsAddModuleVisible => Page == "AddModule";

    public bool IsCatalogEmpty => !CatalogEntries.Any();

    public bool IsCapabilityEditVisible
    {
        get => _isCapabilityEditVisible;
        private set
        {
            if (_isCapabilityEditVisible == value)
                return;
            _isCapabilityEditVisible = value;
            OnPropertyChanged();
        }
    }

    public CapabilityCardViewModel? SelectedCapability
    {
        get => _selectedCapability;
        private set
        {
            if (_selectedCapability == value)
                return;
            _selectedCapability = value;
            OnPropertyChanged();
        }
    }

    internal async Task RefreshAsync()
    {
        foreach (var card in ComponentCards)
        {
            var status = await _adapter.GetComponentStatusAsync(card.Target, _session);
            card.ApplyStatus(status);
        }

        CapabilityCards.Clear();
        foreach (var module in await _capabilities.GetInstalledAsync())
            CapabilityCards.Add(new CapabilityCardViewModel(module, this));
    }

    internal async Task RunComponentActionAsync(ComponentCardViewModel card, InstallerComponentAction action)
    {
        InstallerLog.Write($"{action} {card.Target} starting");
        card.Begin(action);
        try
        {
            await foreach (var progress in _adapter.ExecuteComponentActionAsync(card.Target, action, _session))
                card.ApplyProgress(progress);

            card.ApplyStatus(await _adapter.GetComponentStatusAsync(card.Target, _session));
            InstallerLog.Write($"{action} {card.Target} completed");
        }
        catch (Exception ex)
        {
            InstallerLog.WriteException($"{action} {card.Target}", ex);
            card.Fail($"{ex.Message}\n\nSee log: {InstallerLog.FilePath}");
            try
            {
                var status = await _adapter.GetComponentStatusAsync(card.Target, _session);
                if (status.Kind != InstallerComponentStatusKind.NotInstalled)
                    card.ApplyStatus(status);
            }
            catch (Exception statusEx)
            {
                InstallerLog.WriteException($"{action} {card.Target} status refresh", statusEx);
                card.Fail($"{ex.Message} (Status refresh failed: {statusEx.Message})\n\nSee log: {InstallerLog.FilePath}");
            }
        }
    }

    internal void ShowCapabilityEditor(CapabilityCardViewModel card)
    {
        SelectedCapability = card;
        IsCapabilityEditVisible = true;
    }

    internal async Task SelectCapabilityVersionAsync(CapabilityCardViewModel card, CapabilityVersionViewModel version)
    {
        if (!_capabilities.SupportsInstallActions)
            return;

        var updated = await _capabilities.SelectVersionAsync(card.Module, version.Version);
        card.ApplyModule(updated);
    }

    private async Task ShowAddModuleAsync()
    {
        CatalogEntries.Clear();
        var installedIds = CapabilityCards.Select(card => card.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in await _capabilities.GetCatalogAsync())
            CatalogEntries.Add(new CatalogCapabilityViewModel(entry, installedIds.Contains(entry.Id), _capabilities.SupportsInstallActions, this));

        OnPropertyChanged(nameof(IsCatalogEmpty));
        IsCapabilityEditVisible = false;
        Page = "AddModule";
    }

    private void ShowDashboard()
    {
        Page = "Dashboard";
        IsCapabilityEditVisible = false;
    }

    internal async Task InstallCatalogModuleAsync(CatalogCapabilityViewModel? catalogEntry)
    {
        if (catalogEntry is null || catalogEntry.IsInstalled || !_capabilities.SupportsInstallActions)
            return;

        var installing = CapabilityCardViewModel.Installing(catalogEntry.Entry, this);
        CapabilityCards.Add(installing);
        ShowDashboard();
        await Task.Yield();

        try
        {
            var module = await _capabilities.InstallAsync(catalogEntry.Entry);
            installing.ApplyModule(module);
        }
        catch (Exception ex)
        {
            installing.Fail(ex.Message);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class ComponentCardViewModel : INotifyPropertyChanged
{
    private readonly InstallerViewModel _owner;
    private InstallerComponentStatusKind _status = InstallerComponentStatusKind.NotInstalled;
    private int _progress;
    private string _detail = "Not installed";
    private bool _isBusy;

    public ComponentCardViewModel(
        ProductComponent target,
        string title,
        string description,
        InstallerViewModel owner,
        bool supportsInstallActions)
    {
        Target = target;
        Title = title;
        Description = description;
        _owner = owner;
        SupportsInstallActions = supportsInstallActions;
        InstallCommand = new DelegateCommand(async _ => await _owner.RunComponentActionAsync(this, IsInstalled ? InstallerComponentAction.Update : InstallerComponentAction.Install), _ => SupportsInstallActions && !IsBusy);
        UpdateCommand = new DelegateCommand(async _ => await _owner.RunComponentActionAsync(this, InstallerComponentAction.Update), _ => SupportsInstallActions && !IsBusy && IsInstalled);
        UninstallCommand = new DelegateCommand(async _ => await _owner.RunComponentActionAsync(this, InstallerComponentAction.Uninstall), _ => SupportsInstallActions && !IsBusy && IsInstalled);
        RepairCommand = new DelegateCommand(async _ => await _owner.RunComponentActionAsync(this, InstallerComponentAction.Repair), _ => SupportsInstallActions && !IsBusy && IsInstalled);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ProductComponent Target { get; }

    public string Title { get; }

    public string Description { get; }

    public bool SupportsInstallActions { get; }

    public ICommand InstallCommand { get; }

    public ICommand UpdateCommand { get; }

    public ICommand UninstallCommand { get; }

    public ICommand RepairCommand { get; }

    public string StatusText => _status switch
    {
        InstallerComponentStatusKind.Installed => "Installed",
        InstallerComponentStatusKind.UpdateAvailable => "Update available",
        InstallerComponentStatusKind.Installing => "Installing",
        InstallerComponentStatusKind.Uninstalling => "Uninstalling",
        InstallerComponentStatusKind.Failed => "Failed",
        _ => "Not installed"
    };

    public string StatusBrush => _status switch
    {
        InstallerComponentStatusKind.Installed => "#32d583",
        InstallerComponentStatusKind.UpdateAvailable => "#fdb022",
        InstallerComponentStatusKind.Installing => "#fdb022",
        InstallerComponentStatusKind.Uninstalling => "#fdb022",
        InstallerComponentStatusKind.Failed => "#f97066",
        _ => "#98a2b3"
    };

    public string PrimaryButtonText => SupportsInstallActions ? IsInstalled ? "Update" : "Install" : "Managed by NixOS";

    public bool IsInstalled => _status is InstallerComponentStatusKind.Installed or InstallerComponentStatusKind.UpdateAvailable or InstallerComponentStatusKind.Failed;

    public bool IsBusy => _isBusy;

    public bool IsProgressVisible => IsBusy;

    public int Progress => _progress;

    public string Detail => _detail;

    public void Begin(InstallerComponentAction action)
    {
        _isBusy = true;
        _status = action == InstallerComponentAction.Uninstall
            ? InstallerComponentStatusKind.Uninstalling
            : InstallerComponentStatusKind.Installing;
        _progress = 0;
        _detail = $"{action} started";
        RaiseAll();
    }

    public void ApplyProgress(InstallProgress progress)
    {
        _progress = progress.TotalOperations == 0 ? 0 : (int)Math.Round(progress.CompletedOperations * 100d / progress.TotalOperations);
        _detail = progress.Message;
        RaiseAll();
    }

    public void ApplyStatus(InstallerComponentStatus status)
    {
        _isBusy = false;
        _status = status.Kind;
        _progress = status.IsInstalled ? 100 : 0;
        _detail = status.Message ?? (status.InstalledVersion is null ? StatusText : $"{StatusText} {status.InstalledVersion}");
        RaiseAll();
    }

    public void Fail(string message)
    {
        _isBusy = false;
        _status = InstallerComponentStatusKind.Failed;
        _detail = message;
        RaiseAll();
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(PrimaryButtonText));
        OnPropertyChanged(nameof(IsInstalled));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(IsProgressVisible));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(Detail));
        (InstallCommand as DelegateCommand)?.RaiseCanExecuteChanged();
        (UpdateCommand as DelegateCommand)?.RaiseCanExecuteChanged();
        (UninstallCommand as DelegateCommand)?.RaiseCanExecuteChanged();
        (RepairCommand as DelegateCommand)?.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class CapabilityCardViewModel : INotifyPropertyChanged
{
    private readonly InstallerViewModel _owner;
    private CapabilityModuleStatus _status;
    private string _activeVersion;
    private string _detail;

    private CapabilityCardViewModel(
        InstalledCapabilityModule module,
        CapabilityModuleStatus status,
        InstallerViewModel owner)
    {
        Module = module;
        _status = status;
        _owner = owner;
        _activeVersion = module.ActiveVersion;
        _detail = status == CapabilityModuleStatus.Installing ? "Installing" : $"Active version {_activeVersion}";
        EditCommand = new DelegateCommand(_ => _owner.ShowCapabilityEditor(this), _ => IsInstalled);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public InstalledCapabilityModule Module { get; private set; }

    public string Id => Module.Id;

    public string DisplayName => Module.DisplayName;

    public string Description => Module.Description;

    public string ActiveVersion => _activeVersion;

    public string Detail => _detail;

    public bool IsInstalled => _status == CapabilityModuleStatus.Installed;

    public bool SupportsVersionSelection => _owner.SupportsCapabilityInstallActions;

    public string StatusText => _status switch
    {
        CapabilityModuleStatus.Installed => "Installed",
        CapabilityModuleStatus.Installing => "Installing",
        CapabilityModuleStatus.Failed => "Failed",
        _ => "Not installed"
    };

    public string StatusBrush => _status switch
    {
        CapabilityModuleStatus.Installed => "#32d583",
        CapabilityModuleStatus.Installing => "#fdb022",
        CapabilityModuleStatus.Failed => "#f97066",
        _ => "#98a2b3"
    };

    public ObservableCollection<CapabilityVersionViewModel> Versions { get; } = [];

    public ICommand EditCommand { get; }

    public static CapabilityCardViewModel Installing(CapabilityCatalogEntry entry, InstallerViewModel owner) =>
        new(
            new InstalledCapabilityModule(entry.Id, entry.DisplayName, entry.Description, entry.Versions.First().Version, []),
            CapabilityModuleStatus.Installing,
            owner);

    public CapabilityCardViewModel(InstalledCapabilityModule module, InstallerViewModel owner)
        : this(module, CapabilityModuleStatus.Installed, owner)
    {
        LoadVersions();
    }

    public void ApplyModule(InstalledCapabilityModule module)
    {
        Module = module;
        _status = CapabilityModuleStatus.Installed;
        _activeVersion = module.ActiveVersion;
        _detail = $"Active version {_activeVersion}";
        LoadVersions();
        RaiseAll();
    }

    public void Fail(string message)
    {
        _status = CapabilityModuleStatus.Failed;
        _detail = message;
        RaiseAll();
    }

    private void LoadVersions()
    {
        Versions.Clear();
        foreach (var version in Module.Versions)
            Versions.Add(new CapabilityVersionViewModel(
                version.Version,
                version.Source.ToString(),
                version.Version == Module.ActiveVersion,
                SupportsVersionSelection,
                async item => await _owner.SelectCapabilityVersionAsync(this, item)));
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(ActiveVersion));
        OnPropertyChanged(nameof(Detail));
        OnPropertyChanged(nameof(IsInstalled));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(SupportsVersionSelection));
        (EditCommand as DelegateCommand)?.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class CapabilityVersionViewModel
{
    public CapabilityVersionViewModel(
        string version,
        string source,
        bool isActive,
        bool supportsVersionSelection,
        Func<CapabilityVersionViewModel, Task> select)
    {
        Version = version;
        Source = source;
        IsActive = isActive;
        SelectCommand = new DelegateCommand(async _ => await select(this), _ => supportsVersionSelection && !IsActive);
    }

    public string Version { get; }

    public string Source { get; }

    public bool IsActive { get; }

    public string ActiveText => IsActive ? "Active" : "Available";

    public ICommand SelectCommand { get; }
}

public sealed class CatalogCapabilityViewModel
{
    public CatalogCapabilityViewModel(
        CapabilityCatalogEntry entry,
        bool isInstalled,
        bool supportsInstallActions,
        InstallerViewModel owner)
    {
        Entry = entry;
        IsInstalled = isInstalled;
        SupportsInstallActions = supportsInstallActions;
        InstallCommand = new DelegateCommand(async _ => await owner.InstallCatalogModuleAsync(this), _ => SupportsInstallActions && !IsInstalled);
    }

    public CapabilityCatalogEntry Entry { get; }

    public string DisplayName => Entry.DisplayName;

    public string Description => Entry.Description;

    public string Version => Entry.Versions.FirstOrDefault()?.Version ?? "unknown";

    public bool IsInstalled { get; }

    public bool SupportsInstallActions { get; }

    public string ButtonText => IsInstalled ? "Installed" : SupportsInstallActions ? "Install" : "Managed by NixOS";

    public ICommand InstallCommand { get; }
}

internal sealed class DelegateCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public DelegateCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
