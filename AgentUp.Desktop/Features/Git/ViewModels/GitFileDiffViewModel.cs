using System.Collections.ObjectModel;
using System.Reactive;
using AgentUp.Desktop.Features.Git.Providers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Git.ViewModels;

public sealed class GitFileDiffViewModel : ReactiveObject
{
    private bool _isVisible;
    private bool _isLoading;
    private string _path = string.Empty;
    private string _status = string.Empty;
    private string _content = string.Empty;
    private string _gotoQuery = string.Empty;
    private int _currentIndex;
    private GitFileDiffLineViewModel? _currentLine;

    public GitFileDiffViewModel()
    {
        CloseCommand = ReactiveCommand.Create(Hide);
        JumpCommand = ReactiveCommand.Create<int>(JumpTo);
        JumpToEnteredLineCommand = ReactiveCommand.Create(JumpToEnteredLine);
    }

    public ObservableCollection<GitFileDiffLineViewModel> Lines { get; } = [];
    public ObservableCollection<GitFileDiffHunkViewModel> Hunks { get; } = [];

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

    public string GotoQuery
    {
        get => _gotoQuery;
        set => this.RaiseAndSetIfChanged(ref _gotoQuery, value);
    }

    public int CurrentIndex
    {
        get => _currentIndex;
        private set => this.RaiseAndSetIfChanged(ref _currentIndex, value);
    }

    public GitFileDiffLineViewModel? CurrentLine
    {
        get => _currentLine;
        set
        {
            if (_currentLine == value)
                return;
            if (_currentLine is not null)
                _currentLine.IsCurrent = false;
            _currentLine = value;
            if (_currentLine is not null)
            {
                _currentLine.IsCurrent = true;
                CurrentIndex = _currentLine.Index;
            }

            this.RaisePropertyChanged();
        }
    }

    public bool HasHunks => Hunks.Count > 0;

    public ReactiveCommand<Unit, Unit> CloseCommand { get; }
    public ReactiveCommand<int, Unit> JumpCommand { get; }
    public ReactiveCommand<Unit, Unit> JumpToEnteredLineCommand { get; }

    internal void ShowLoading(string path, string status)
    {
        Path = path;
        Status = status;
        Content = string.Empty;
        GotoQuery = string.Empty;
        ReplaceLines([]);
        IsLoading = true;
        IsVisible = true;
    }

    internal void ShowDiff(string path, string status, string content)
    {
        Path = path;
        Status = status;
        Content = content;
        GotoQuery = string.Empty;
        var parsed = GitFileViewerProvider.ParseDiff(content);
        ReplaceLines(parsed.Select(line => new GitFileDiffLineViewModel(path, line)).ToList());
        foreach (var hunk in GitFileViewerProvider.Hunks(parsed))
            Hunks.Add(new GitFileDiffHunkViewModel(hunk.Label, hunk.LineIndex));
        this.RaisePropertyChanged(nameof(HasHunks));
        IsLoading = false;
        IsVisible = true;
        if (Lines.Count > 0)
            CurrentLine = Lines[0];
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
        GotoQuery = string.Empty;
        ReplaceLines([]);
    }

    private void JumpTo(int index)
    {
        if (index < 0 || index >= Lines.Count)
            return;
        CurrentLine = Lines[index];
    }

    private void JumpToEnteredLine()
    {
        var parsed = GitFileViewerProvider.ParseDiff(Content);
        var index = GitFileViewerProvider.JumpIndex(parsed, GotoQuery);
        if (index is int line)
            JumpTo(line);
    }

    private void ReplaceLines(IReadOnlyList<GitFileDiffLineViewModel> lines)
    {
        CurrentLine = null;
        Lines.Clear();
        Hunks.Clear();
        foreach (var line in lines)
            Lines.Add(line);
        CurrentIndex = 0;
        this.RaisePropertyChanged(nameof(HasHunks));
    }
}
