using System.Collections.ObjectModel;
using System.Reactive;
using AgentUp.Desktop.Features.Interface.Controllers;
using AgentUp.Desktop.Features.Interface.Interfaces;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Services;
using AgentUp.Desktop.Shared.Providers;
using Avalonia.Media.Imaging;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Interface.ViewModels;

/// <summary>
/// The backlog of product decisions the agents have taken, for a product owner to judge.
///
/// It is the same backlog the phone shows, read over the same endpoints, laid out for a mouse: a
/// repository is chosen from tiles, a category from a row, and one decision is judged at a time
/// with the next and previous ones a click away rather than a swipe.
/// </summary>
public sealed class TrainingViewModel : ReactiveObject
{
    private readonly TrainingController _training;
    private readonly IDecisionExportSink? _export;
    private readonly List<ProductDecision> _decisions = [];
    private DecisionRepositoryRowViewModel? _repository;
    private string _category = "product-vision";
    private int _index;
    private string _note = string.Empty;
    private string _verdict = "pending";
    private string? _error;
    private string? _exported;
    private Bitmap? _picture;
    private string? _pictured;
    private bool _read;

    public TrainingViewModel(TrainingController training, IDecisionExportSink? export = null)
    {
        _training = training;
        _export = export;
        OpenRepositoryCommand = ReactiveCommand.CreateFromTask<DecisionRepositoryRowViewModel>(OpenAsync);
        CloseRepositoryCommand = ReactiveCommand.Create(Close);
        ShowCategoryCommand = ReactiveCommand.Create<string>(ShowCategory);
        NextCommand = ReactiveCommand.Create(() => Move(1));
        PreviousCommand = ReactiveCommand.Create(() => Move(-1));
        GoodCommand = ReactiveCommand.Create(() => Verdict = "good");
        BadCommand = ReactiveCommand.Create(() => Verdict = "bad");
        SubmitCommand = ReactiveCommand.CreateFromTask(SubmitAsync);
        ExportCommand = ReactiveCommand.CreateFromTask(ExportAsync);
    }

    /// <summary>Every repository an agent has decided something in.</summary>
    public ObservableCollection<DecisionRepositoryRowViewModel> Repositories { get; } = [];

    /// <summary>The three categories of the repository that is open.</summary>
    public ObservableCollection<DecisionCategoryRowViewModel> Categories { get; } = [];

    public ReactiveCommand<DecisionRepositoryRowViewModel, Unit> OpenRepositoryCommand { get; }

    public ReactiveCommand<Unit, Unit> CloseRepositoryCommand { get; }

    public ReactiveCommand<string, Unit> ShowCategoryCommand { get; }

    public ReactiveCommand<Unit, Unit> NextCommand { get; }

    public ReactiveCommand<Unit, Unit> PreviousCommand { get; }

    public ReactiveCommand<Unit, string> GoodCommand { get; }

    public ReactiveCommand<Unit, string> BadCommand { get; }

    public ReactiveCommand<Unit, Unit> SubmitCommand { get; }

    public ReactiveCommand<Unit, Unit> ExportCommand { get; }

    /// <summary>True while the tiles are what the surface shows.</summary>
    public bool ShowsRepositories => _repository is null;

    /// <summary>True while one repository's decisions are being read through.</summary>
    public bool ShowsDecisions => _repository is not null;

    public string RepositoryName => _repository?.Name ?? string.Empty;

    public bool HasRepositories => Repositories.Count > 0;

    public string EmptyHint => _read
        ? "No decisions yet. When an agent takes a product decision it records it here, and you judge it in your own time."
        : "Reading the decisions the agents have taken…";

    public string ErrorMessage => _error ?? string.Empty;

    public bool HasError => _error is { Length: > 0 };

    /// <summary>The decisions of the category on screen.</summary>
    public IReadOnlyList<ProductDecision> Shown => TrainingService.InCategory(_decisions, _category);

    public bool HasShown => Shown.Count > 0;

    public string EmptyCategoryHint => "No decisions have been recorded in this part of the product.";

    /// <summary>The decision being judged, or null where the category is empty.</summary>
    public ProductDecision? Decision => Shown.Count == 0 ? null : Shown[Math.Min(_index, Shown.Count - 1)];

    public string Headline => Decision?.Headline ?? string.Empty;

    public string Detail => Decision?.Detail ?? string.Empty;

    public string Evidence => Decision?.Evidence ?? string.Empty;

    public string DecidedBy => Decision is null
        ? string.Empty
        : $"{Decision.Agent} · {Categories.FirstOrDefault(category => category.Id == Decision.Category)?.Label ?? Decision.Category}";

    public string Position => Shown.Count == 0 ? string.Empty : $"{Math.Min(_index, Shown.Count - 1) + 1} of {Shown.Count}";

    public bool CanGoBack => _index > 0;

    public bool CanGoOn => _index < Shown.Count - 1;

    /// <summary>What the reader has chosen for this decision, before they submit it.</summary>
    public string Verdict
    {
        get => _verdict;
        private set
        {
            this.RaiseAndSetIfChanged(ref _verdict, value);
            this.RaisePropertyChanged(nameof(ChoseGood));
            this.RaisePropertyChanged(nameof(ChoseBad));
            this.RaisePropertyChanged(nameof(CanSubmit));
            this.RaisePropertyChanged(nameof(VerdictLabel));
        }
    }

    public bool ChoseGood => Verdict == "good";

    public bool ChoseBad => Verdict == "bad";

    public bool CanSubmit => Verdict != "pending";

    /// <summary>The picture behind this decision, or null where it has none.</summary>
    public Bitmap? EvidencePicture
    {
        get => _picture;
        private set
        {
            this.RaiseAndSetIfChanged(ref _picture, value);
            this.RaisePropertyChanged(nameof(HasEvidencePicture));
        }
    }

    public bool HasEvidencePicture => EvidencePicture is not null;

    /// <summary>What the reader has chosen, said back to them so the buttons are not the only clue.</summary>
    public string VerdictLabel => Verdict switch
    {
        "good" => "Your call: a good one.",
        "bad" => "Your call: a bad one.",
        _ => "No call yet."
    };

    /// <summary>What came of the last export, which is the only sign a file was written.</summary>
    public string ExportMessage => _exported ?? string.Empty;

    public bool HasExportMessage => _exported is { Length: > 0 };

    public string Note
    {
        get => _note;
        set => this.RaiseAndSetIfChanged(ref _note, value);
    }

    /// <summary>Reads the tiles. Everything else follows from opening one.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var found = await _training.RepositoriesAsync(cancellationToken);
            Repositories.Clear();
            foreach (var repository in found)
                Repositories.Add(new DecisionRepositoryRowViewModel(repository));
            _error = null;
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            _error = failure.Message;
        }

        _read = true;
        RaiseShell();
    }

    private async Task OpenAsync(DecisionRepositoryRowViewModel repository)
    {
        _repository = repository;
        Categories.Clear();
        foreach (var category in repository.Categories)
            Categories.Add(category);

        try
        {
            var found = await _training.DecisionsAsync(repository.Owner, repository.Repository);
            _decisions.Clear();
            _decisions.AddRange(found);
            _error = null;
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            _error = failure.Message;
        }

        _category = Categories.FirstOrDefault()?.Id ?? "product-vision";
        Seek();
        RaiseShell();
    }

    private void Close()
    {
        _repository = null;
        _decisions.Clear();
        Categories.Clear();
        RaiseShell();
        _ = LoadAsync();
    }

    private void ShowCategory(string category)
    {
        _category = category;
        Seek();
        RaiseCard();
    }

    private void Move(int direction)
    {
        if (Shown.Count == 0) return;
        _index = Math.Clamp(_index + direction, 0, Shown.Count - 1);
        Adopt();
        RaiseCard();
    }

    /// <summary>
    /// Sends the verdict and moves on. The reader has already finished with this card, so the
    /// backlog is told and the next one is put up rather than waiting for the Server to agree.
    /// </summary>
    private async Task SubmitAsync()
    {
        var decision = Decision;
        if (decision is null || Verdict == "pending") return;

        // The call and the note are taken before the deck moves: moving adopts the next card's
        // verdict, so reading them afterwards would send that card's answer for this one.
        var verdict = Verdict;
        var note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();
        Replace(decision with { Verdict = verdict, Note = note, ReviewedAt = DateTimeOffset.UtcNow });
        Move(1);

        try
        {
            await _training.ReviewAsync(decision.Id, verdict, note);
            _error = null;
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            // The verdict did not land, so the card goes back to waiting rather than lying.
            Replace(decision);
            _error = failure.Message;
        }

        RaiseShell();
    }

    /// <summary>
    /// Writes the judged backlog out as training material.
    ///
    /// Only judged decisions go, because a decision nobody has ruled on teaches nothing: it is the
    /// verdict, not the decision, that is the lesson.
    /// </summary>
    private async Task ExportAsync()
    {
        if (_export is null)
        {
            _exported = "Saving an export is not available here.";
            RaiseExport();
            return;
        }

        try
        {
            var jsonl = await _training.ExportAsync();
            var count = TrainingService.ExportedCount(jsonl);
            if (count == 0)
            {
                _exported = "Nothing to export yet: judge some decisions first.";
                RaiseExport();
                return;
            }

            var saved = await _export.SaveAsync(TrainingService.ExportFileName(DateTimeOffset.Now), jsonl);
            _exported = saved ? $"Exported {count} judged decisions." : string.Empty;
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException or IOException)
        {
            _exported = $"Could not export the decisions: {failure.Message}";
        }

        RaiseExport();
    }

    private void RaiseExport()
    {
        this.RaisePropertyChanged(nameof(ExportMessage));
        this.RaisePropertyChanged(nameof(HasExportMessage));
    }

    private void Replace(ProductDecision decision)
    {
        var index = _decisions.FindIndex(item => item.Id == decision.Id);
        if (index >= 0) _decisions[index] = decision;
    }

    /// <summary>Opens a category on the first decision nobody has judged.</summary>
    private void Seek()
    {
        _index = TrainingService.FirstUnjudged(Shown);
        Adopt();
    }

    /// <summary>Takes the verdict already on the card, so judging again starts from what was said.</summary>
    private void Adopt()
    {
        Verdict = Decision?.Verdict ?? "pending";
        Note = Decision?.Note ?? string.Empty;
    }

    private void RaiseShell()
    {
        this.RaisePropertyChanged(nameof(ShowsRepositories));
        this.RaisePropertyChanged(nameof(ShowsDecisions));
        this.RaisePropertyChanged(nameof(RepositoryName));
        this.RaisePropertyChanged(nameof(HasRepositories));
        this.RaisePropertyChanged(nameof(EmptyHint));
        this.RaisePropertyChanged(nameof(ErrorMessage));
        this.RaisePropertyChanged(nameof(HasError));
        RaiseCard();
    }

    private void RaiseCard()
    {
        foreach (var category in Categories)
            category.Recount(TrainingService.Pending(TrainingService.InCategory(_decisions, category.Id)));

        this.RaisePropertyChanged(nameof(Shown));
        this.RaisePropertyChanged(nameof(HasShown));
        this.RaisePropertyChanged(nameof(Decision));
        this.RaisePropertyChanged(nameof(Headline));
        this.RaisePropertyChanged(nameof(Detail));
        this.RaisePropertyChanged(nameof(Evidence));
        this.RaisePropertyChanged(nameof(DecidedBy));
        this.RaisePropertyChanged(nameof(Position));
        this.RaisePropertyChanged(nameof(CanGoBack));
        this.RaisePropertyChanged(nameof(CanGoOn));
        _ = PictureAsync();
    }

    /// <summary>
    /// Reads the card's evidence picture, if it has one.
    ///
    /// A card is read far longer than it takes to fetch, and the same card is often come back to,
    /// so the picture is read once per card and the id it belongs to is remembered rather than
    /// re-read on every property change the deck raises.
    /// </summary>
    private async Task PictureAsync()
    {
        var evidenceId = Decision?.EvidenceImages.FirstOrDefault()?.EvidenceId;
        if (evidenceId == _pictured) return;
        _pictured = evidenceId;

        if (evidenceId is null)
        {
            EvidencePicture = null;
            return;
        }

        try
        {
            var bytes = await _training.EvidenceAsync(evidenceId);
            if (_pictured != evidenceId) return;
            EvidencePicture = bytes is null ? null : new Bitmap(new MemoryStream(bytes));
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException or ArgumentException)
        {
            // A missing picture is not worth an error line: the decision's own words carry it.
            EvidencePicture = null;
        }
    }
}

/// <summary>One repository's tile.</summary>
public sealed class DecisionRepositoryRowViewModel(DecisionRepository repository)
{
    public string Owner { get; } = repository.Owner;

    public string Repository { get; } = repository.Repository;

    public string Name { get; } = repository.Key;

    public string TestId { get; } = DesktopAutomationIds.TrainRepository(repository.Key);

    public string Waiting { get; } = repository.Pending > 0 ? $"{repository.Pending} waiting" : "All judged";

    public bool HasWaiting { get; } = repository.Pending > 0;

    public string Judged { get; } = $"{repository.Reviewed} judged";

    public IReadOnlyList<DecisionCategoryRowViewModel> Categories { get; } =
        [.. repository.Categories.Select(category => new DecisionCategoryRowViewModel(category))];
}

/// <summary>One category in the row under a repository's name.</summary>
public sealed class DecisionCategoryRowViewModel(DecisionCategoryCount category) : ReactiveObject
{
    private int _pending = category.Pending;

    public string Id { get; } = category.Category;

    public string Label { get; } = category.Label;

    public string TestId { get; } = DesktopAutomationIds.TrainCategory(category.Category);

    public string Count => _pending > 0 ? $"{_pending}" : string.Empty;

    public bool HasCount => _pending > 0;

    /// <summary>Counted from what is loaded, so judging a card changes the number straight away.</summary>
    public void Recount(int pending)
    {
        _pending = pending;
        this.RaisePropertyChanged(nameof(Count));
        this.RaisePropertyChanged(nameof(HasCount));
    }
}
