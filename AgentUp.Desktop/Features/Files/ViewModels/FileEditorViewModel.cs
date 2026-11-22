using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using AgentUp.Desktop.Features.Files.Controllers;
using AgentUp.Desktop.Features.Files.DTOs;
using AgentUp.Desktop.Features.Files.Providers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Files.ViewModels;

/// <summary>
/// One file opened as a page: the whole text, editable with the plain controls a text box gives
/// (typing, selection, undo and redo, Tab), a line gutter, the caret position, and Save. A save
/// names the version the edit started from, so an agent's change in the meantime is never
/// overwritten: the Server refuses it and the page offers Reload instead.
/// </summary>
public sealed class FileEditorViewModel : ReactiveObject
{
    private const string ReadOnlyCopy = "Read only. This sign-in cannot change files.";
    private readonly WorkspaceFilesController _files;
    private readonly Subject<Unit> _closed = new();
    private string? _workspaceId;
    private string _path = string.Empty;
    private string? _status;
    private string _text = string.Empty;
    private string _original = string.Empty;
    private string _version = string.Empty;
    private bool _isOpen;
    private bool _isLoading;
    private bool _isSaving;
    private bool _hasContent;
    private bool _isConflict;
    private bool _isConfirmingDiscard;
    private bool _canWriteGit = true;
    private string? _notice;
    private string? _error;
    private int _caretIndex;
    private int _lineCount = 1;
    private string _lineNumbers = "1";
    private long _generation;

    public FileEditorViewModel(WorkspaceFilesController files)
    {
        _files = files;
        var canSave = this.WhenAnyValue(x => x.IsDirty, x => x.CanEdit, x => x.IsSaving, x => x.IsConflict,
            (dirty, editable, saving, conflict) => dirty && editable && !saving && !conflict);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, canSave);
        RevertCommand = ReactiveCommand.Create(Revert, this.WhenAnyValue(x => x.IsDirty));
        ReloadCommand = ReactiveCommand.CreateFromTask(ReloadAsync);
        BackCommand = ReactiveCommand.Create(Back);
        DiscardCommand = ReactiveCommand.Create(Close);
        KeepEditingCommand = ReactiveCommand.Create(() => { IsConfirmingDiscard = false; });
    }

    /// <summary>Fires when the page closes, so the tree can drop its selection.</summary>
    public IObservable<Unit> Closed => _closed.AsObservable();

    public string Path
    {
        get => _path;
        private set
        {
            this.RaiseAndSetIfChanged(ref _path, value);
            this.RaisePropertyChanged(nameof(FileName));
        }
    }

    public string FileName => System.IO.Path.GetFileName(Path);

    public string StatusLabel => WorkspaceFileTextProvider.StatusLabel(_status);

    public string Text
    {
        get => _text;
        set
        {
            var next = value ?? string.Empty;
            if (_text == next)
                return;
            this.RaiseAndSetIfChanged(ref _text, next);
            RefreshLines();
            RaiseState();
        }
    }

    public int CaretIndex
    {
        get => _caretIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _caretIndex, value);
            this.RaisePropertyChanged(nameof(CaretLabel));
        }
    }

    public string CaretLabel => WorkspaceFileTextProvider.CaretLabel(Text, CaretIndex);

    public string LineNumbers
    {
        get => _lineNumbers;
        private set => this.RaiseAndSetIfChanged(ref _lineNumbers, value);
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => this.RaiseAndSetIfChanged(ref _isOpen, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isSaving, value);
            RaiseState();
        }
    }

    public bool IsDirty => _hasContent && !string.Equals(_text, _original, StringComparison.Ordinal);

    /// <summary>True when the page shows text: false for a binary file, one too large to send, or a failed read.</summary>
    public bool HasContent
    {
        get => _hasContent;
        private set
        {
            this.RaiseAndSetIfChanged(ref _hasContent, value);
            this.RaisePropertyChanged(nameof(CanEdit));
            this.RaisePropertyChanged(nameof(IsReadOnly));
        }
    }

    public bool CanWriteGit
    {
        get => _canWriteGit;
        set
        {
            this.RaiseAndSetIfChanged(ref _canWriteGit, value);
            this.RaisePropertyChanged(nameof(CanEdit));
            this.RaisePropertyChanged(nameof(IsReadOnly));
            RaiseState();
        }
    }

    public bool CanEdit => HasContent && CanWriteGit;

    public bool IsReadOnly => !CanEdit;

    public bool IsConflict
    {
        get => _isConflict;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isConflict, value);
            RaiseState();
        }
    }

    public bool IsConfirmingDiscard
    {
        get => _isConfirmingDiscard;
        private set => this.RaiseAndSetIfChanged(ref _isConfirmingDiscard, value);
    }

    /// <summary>Why this file shows no editor: binary, too large, or not readable.</summary>
    public string? Notice
    {
        get => _notice;
        private set
        {
            this.RaiseAndSetIfChanged(ref _notice, value);
            this.RaisePropertyChanged(nameof(HasNotice));
        }
    }

    public bool HasNotice => Notice is not null;

    /// <summary>What went wrong with the last save, or the conflict that stopped it.</summary>
    public string? Error
    {
        get => _error;
        private set
        {
            this.RaiseAndSetIfChanged(ref _error, value);
            RaiseState();
        }
    }

    /// <summary>The footer's one line: saved, unsaved, saving, read only, or what failed.</summary>
    public string StateLabel => Error
        ?? (IsSaving ? "Saving…"
            : IsDirty ? "Unsaved changes"
            : !CanWriteGit && HasContent ? ReadOnlyCopy
            : "Saved");

    public bool IsStateDirty => Error is null && (IsDirty || IsSaving);

    public bool IsStateError => Error is not null;

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> RevertCommand { get; }
    public ReactiveCommand<Unit, Unit> ReloadCommand { get; }
    public ReactiveCommand<Unit, Unit> BackCommand { get; }
    public ReactiveCommand<Unit, Unit> DiscardCommand { get; }
    public ReactiveCommand<Unit, Unit> KeepEditingCommand { get; }

    public async Task OpenAsync(string workspaceId, string path)
    {
        _workspaceId = workspaceId;
        Path = path;
        IsOpen = true;
        IsConfirmingDiscard = false;
        await LoadAsync();
    }

    /// <summary>Closes the page without saving. The tree is what shows next.</summary>
    public void Close()
    {
        _generation++;
        IsOpen = false;
        IsConfirmingDiscard = false;
        Reset();
        _closed.OnNext(Unit.Default);
    }

    private void Back()
    {
        if (IsDirty)
        {
            IsConfirmingDiscard = true;
            return;
        }
        Close();
    }

    private async Task ReloadAsync()
    {
        IsConfirmingDiscard = false;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var generation = ++_generation;
        var workspaceId = _workspaceId;
        if (workspaceId is null)
            return;
        IsLoading = true;
        Reset();
        try
        {
            var file = await _files.GetFileAsync(workspaceId, Path);
            if (generation != _generation)
                return;
            Apply(file);
        }
        catch (HttpRequestException ex)
        {
            if (generation == _generation)
                Notice = ex.Message;
        }
        finally
        {
            if (generation == _generation)
                IsLoading = false;
        }
    }

    private void Apply(WorkspaceFileContentDto? file)
    {
        if (file is null)
        {
            Notice = "This file is no longer in the workspace.";
            return;
        }

        _status = file.Status;
        _version = file.Version;
        this.RaisePropertyChanged(nameof(StatusLabel));
        if (file.Content is null)
        {
            Notice = file.IsTooLarge
                ? $"This file is {WorkspaceFileTextProvider.SizeLabel(file.Size)}, more than the Server sends to a client."
                : "This is a binary file. Files shows text only.";
            return;
        }

        _original = file.Content;
        HasContent = true;
        _text = string.Empty;
        Text = file.Content;
        CaretIndex = 0;
    }

    private async Task SaveAsync()
    {
        var workspaceId = _workspaceId;
        if (workspaceId is null)
            return;
        var generation = _generation;
        IsSaving = true;
        Error = null;
        try
        {
            var result = await _files.SaveAsync(workspaceId, Path, Text, _version);
            if (generation != _generation)
                return;
            if (result.Saved is { } saved)
            {
                _version = saved.Version;
                _status = saved.Status;
                _original = Text;
                this.RaisePropertyChanged(nameof(StatusLabel));
                RaiseState();
                return;
            }
            IsConflict = result.Conflict;
            Error = result.Error ?? "The file was not saved.";
        }
        catch (HttpRequestException ex)
        {
            Error = ex.Message;
        }
        finally
        {
            if (generation == _generation)
                IsSaving = false;
        }
    }

    private void Revert()
    {
        Text = _original;
        Error = null;
        IsConfirmingDiscard = false;
    }

    private void Reset()
    {
        HasContent = false;
        Notice = null;
        Error = null;
        IsConflict = false;
        _status = null;
        _original = string.Empty;
        _version = string.Empty;
        _text = string.Empty;
        this.RaisePropertyChanged(nameof(Text));
        this.RaisePropertyChanged(nameof(StatusLabel));
        CaretIndex = 0;
        RefreshLines();
        RaiseState();
    }

    private void RefreshLines()
    {
        var count = WorkspaceFileTextProvider.LineCount(_text);
        if (count == _lineCount && LineNumbers.Length > 0)
            return;
        _lineCount = count;
        LineNumbers = WorkspaceFileTextProvider.LineNumbers(count);
    }

    private void RaiseState()
    {
        this.RaisePropertyChanged(nameof(IsDirty));
        this.RaisePropertyChanged(nameof(StateLabel));
        this.RaisePropertyChanged(nameof(IsStateDirty));
        this.RaisePropertyChanged(nameof(IsStateError));
        this.RaisePropertyChanged(nameof(CaretLabel));
    }
}
