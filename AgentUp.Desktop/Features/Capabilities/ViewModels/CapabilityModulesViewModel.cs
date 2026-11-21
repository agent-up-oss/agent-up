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

    /// <summary>Re-reads the Server's modules without opening the dialog, for surfaces that list them inline.</summary>
    public Task RefreshAsync() => ReloadAsync();

    public async Task EnableAsync(string id)
    {
        Status = $"Enabling {id}…";
        try
        {
            await _controller.EnableAsync(id);
        }
        catch (HttpRequestException exception)
        {
            // The Server refuses to enable a capability whose software it cannot find (409); the
            // reload below shows why on the card, so the refusal must not escape the command.
            Status = exception.Message;
        }

        await ReloadAsync();
    }

    public async Task SaveSettingsAsync(string id, IReadOnlyDictionary<string, string> values)
    {
        if (values.Count == 0)
            return;
        Status = $"Saving {id} settings…";
        try
        {
            await _controller.UpdateSettingsAsync(id, values);
        }
        catch (HttpRequestException exception)
        {
            Status = exception.Message;
            return;
        }

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
            Status = modules.Count == 0 ? "The Server reports no capabilities." : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException or System.Text.Json.JsonException)
        {
            Status = exception.Message;
        }
    }
}
