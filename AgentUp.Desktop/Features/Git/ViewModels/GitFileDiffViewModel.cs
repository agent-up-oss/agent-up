using System.Reactive;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Git.ViewModels;

public sealed class GitFileDiffViewModel : ReactiveObject
{
    private bool _isVisible;
    private bool _isLoading;
    private string _path = string.Empty;
    private string _status = string.Empty;
    private string _content = string.Empty;

    public GitFileDiffViewModel()
    {
        CloseCommand = ReactiveCommand.Create(Hide);
    }

    public bool IsVisible
    {
        get => _isVisible;
        private set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public string Path
    {
        get => _path;
        private set => this.RaiseAndSetIfChanged(ref _path, value);
    }

    public string Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public string Content
    {
        get => _content;
        private set => this.RaiseAndSetIfChanged(ref _content, value);
    }

    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    internal void ShowLoading(string path, string status)
    {
        Path = path;
        Status = status;
        Content = string.Empty;
        IsLoading = true;
        IsVisible = true;
    }

    internal void ShowDiff(string path, string status, string content)
    {
        Path = path;
        Status = status;
        Content = content;
        IsLoading = false;
        IsVisible = true;
    }

    internal void ShowBinary(string path, string status)
        => ShowDiff(path, status, "This file is binary; Agent-Up does not render a text diff for it.");

    internal void ShowError(string path, string message)
        => ShowDiff(path, string.Empty, message);

    internal void Hide()
    {
        IsVisible = false;
        IsLoading = false;
        Path = string.Empty;
        Status = string.Empty;
        Content = string.Empty;
    }
}
