using System.Collections.ObjectModel;
using System.Net.Http;
using System.Reactive;
using System.Text.Json;
using AgentUp.Desktop.Features.Capabilities.Controllers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Capabilities.ViewModels;

public sealed class CapabilityModulesViewModel : ReactiveObject
{
    private readonly CapabilityModulesController _controller;
    private bool _isOpen;
    private string? _status;

    public CapabilityModulesViewModel(CapabilityModulesController controller)
    {
        _controller = controller;
        OpenCommand = ReactiveCommand.CreateFromTask(OpenAsync);
        CloseCommand = ReactiveCommand.Create(Close);
    }

    public ObservableCollection<CapabilityModuleCardViewModel> Modules { get; } = [];

    public bool IsOpen
    {
        get => _isOpen;
        private set => this.RaiseAndSetIfChanged(ref _isOpen, value);
    }

    public string? Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public ReactiveCommand<Unit, Unit> OpenCommand { get; }

    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    public async Task OpenAsync()
    {
        IsOpen = true;
        await ReloadAsync();
    }

    public void Close() => IsOpen = false;

    public async Task EnableAsync(string id, string version)
    {
        Status = $"Enabling {id}…";
        await _controller.EnableAsync(id, version);
        await ReloadAsync();
    }

    public async Task DisableAsync(string id)
    {
        Status = $"Disabling {id}…";
        await _controller.DisableAsync(id);
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            Status = "Loading capability modules…";
            var modules = await _controller.ListAsync();
            Modules.Clear();
            foreach (var module in modules)
                Modules.Add(new CapabilityModuleCardViewModel(module, this));
            Status = modules.Count == 0 ? "No capability modules in the Server registry." : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException or System.Text.Json.JsonException)
        {
            Status = exception.Message;
        }
    }
}
