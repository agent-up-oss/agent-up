using System.Reactive.Linq;
using AgentUp.Desktop.Features.Interface.Controllers;
using AgentUp.Desktop.Features.Interface.Interfaces;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Services;
using AgentUp.Desktop.Features.Interface.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Interface.Unit;

[TestFixture]
public sealed class TrainingViewModelTests
{
    [Test]
    public async Task LoadAsync_showsOneTilePerRepositoryTheAgentsDecidedIn()
    {
        var api = new FakeTrainingApi { Repositories = [Repository("example", "repository", 2, 1)] };
        var training = ViewModel(api);

        await training.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(training.Repositories.Single().Name, Is.EqualTo("example/repository"));
            Assert.That(training.Repositories.Single().Waiting, Is.EqualTo("2 waiting"));
            Assert.That(training.ShowsRepositories, Is.True);
            Assert.That(training.ShowsDecisions, Is.False);
        });
    }

    [Test]
    public async Task LoadAsync_saysSoWhereNobodyHasRecordedAnything()
    {
        var training = ViewModel(new FakeTrainingApi());

        await training.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(training.HasRepositories, Is.False);
            Assert.That(training.EmptyHint, Does.Contain("No decisions yet"));
        });
    }

    [Test]
    public async Task LoadAsync_saysWhatWentWrongWhereTheServerCouldNotBeReached()
    {
        var api = new FakeTrainingApi { Failure = new HttpRequestException("no route") };
        var training = ViewModel(api);

        await training.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(training.HasError, Is.True);
            Assert.That(training.ErrorMessage, Is.EqualTo("no route"));
        });
    }

    [Test]
    public async Task OpenRepository_opensTheFirstDecisionNobodyHasJudged()
    {
        var api = new FakeTrainingApi
        {
            Repositories = [Repository("example", "repository", 1, 1)],
            Decisions = [Decision("1", verdict: "good"), Decision("2")]
        };
        var training = ViewModel(api);
        await training.LoadAsync();

        await Open(training);

        Assert.Multiple(() =>
        {
            Assert.That(training.ShowsDecisions, Is.True);
            Assert.That(training.Decision!.Id, Is.EqualTo("2"));
            Assert.That(training.Position, Is.EqualTo("2 of 2"));
        });
    }

    [Test]
    public async Task OpenRepository_takesTheVerdictAlreadyOnACardRatherThanAskingAgain()
    {
        var api = new FakeTrainingApi
        {
            Repositories = [Repository("example", "repository", 0, 1)],
            Decisions = [Decision("1", verdict: "bad", note: "shipped too early")]
        };
        var training = ViewModel(api);
        await training.LoadAsync();

        await Open(training);

        Assert.Multiple(() =>
        {
            Assert.That(training.ChoseBad, Is.True);
            Assert.That(training.Note, Is.EqualTo("shipped too early"));
            Assert.That(training.VerdictLabel, Does.Contain("bad"));
        });
    }

    [Test]
    public async Task ShowCategory_readsOnlyTheDecisionsOfThatPartOfTheProduct()
    {
        var api = new FakeTrainingApi
        {
            Repositories = [Repository("example", "repository", 2, 0)],
            Decisions = [Decision("1"), Decision("2", category: "gui-decisions")]
        };
        var training = ViewModel(api);
        await training.LoadAsync();
        await Open(training);

        training.ShowCategoryCommand.Execute("gui-decisions").Subscribe();

        Assert.Multiple(() =>
        {
            Assert.That(training.Shown.Single().Id, Is.EqualTo("2"));
            Assert.That(training.Position, Is.EqualTo("1 of 1"));
            Assert.That(training.CanGoOn, Is.False);
        });
    }

    [Test]
    public async Task Submit_sendsTheVerdictAndMovesOn()
    {
        var api = new FakeTrainingApi
        {
            Repositories = [Repository("example", "repository", 2, 0)],
            Decisions = [Decision("1"), Decision("2")]
        };
        var training = ViewModel(api);
        await training.LoadAsync();
        await Open(training);

        training.GoodCommand.Execute().Subscribe();
        training.Note = "  right call  ";
        await training.SubmitCommand.Execute();

        Assert.Multiple(() =>
        {
            Assert.That(api.Reviewed.Single(), Is.EqualTo(("1", "good", "right call")));
            Assert.That(training.Decision!.Id, Is.EqualTo("2"));
        });
    }

    [Test]
    public async Task Submit_putsTheCardBackToWaitingWhereTheVerdictDidNotLand()
    {
        var api = new FakeTrainingApi
        {
            Repositories = [Repository("example", "repository", 1, 0)],
            Decisions = [Decision("1")],
            ReviewFailure = new HttpRequestException("gone")
        };
        var training = ViewModel(api);
        await training.LoadAsync();
        await Open(training);

        training.BadCommand.Execute().Subscribe();
        await training.SubmitCommand.Execute();

        Assert.Multiple(() =>
        {
            Assert.That(training.Shown.Single().Reviewed, Is.False);
            Assert.That(training.HasError, Is.True);
        });
    }

    [Test]
    public async Task Submit_doesNothingUntilTheReaderHasMadeACall()
    {
        var api = new FakeTrainingApi
        {
            Repositories = [Repository("example", "repository", 1, 0)],
            Decisions = [Decision("1")]
        };
        var training = ViewModel(api);
        await training.LoadAsync();
        await Open(training);

        await training.SubmitCommand.Execute();

        Assert.Multiple(() =>
        {
            Assert.That(training.CanSubmit, Is.False);
            Assert.That(api.Reviewed, Is.Empty);
        });
    }

    [Test]
    public async Task CloseRepository_goesBackToTheTiles()
    {
        var api = new FakeTrainingApi
        {
            Repositories = [Repository("example", "repository", 1, 0)],
            Decisions = [Decision("1")]
        };
        var training = ViewModel(api);
        await training.LoadAsync();
        await Open(training);

        training.CloseRepositoryCommand.Execute().Subscribe();

        Assert.Multiple(() =>
        {
            Assert.That(training.ShowsRepositories, Is.True);
            Assert.That(training.Categories, Is.Empty);
        });
    }

    [Test]
    public async Task Next_andPrevious_walkTheDeckWithoutWrapping()
    {
        var api = new FakeTrainingApi
        {
            Repositories = [Repository("example", "repository", 2, 0)],
            Decisions = [Decision("1"), Decision("2")]
        };
        var training = ViewModel(api);
        await training.LoadAsync();
        await Open(training);

        training.NextCommand.Execute().Subscribe();
        training.NextCommand.Execute().Subscribe();

        Assert.Multiple(() =>
        {
            Assert.That(training.Decision!.Id, Is.EqualTo("2"));
            Assert.That(training.CanGoOn, Is.False);
            Assert.That(training.CanGoBack, Is.True);
        });

        training.PreviousCommand.Execute().Subscribe();
        Assert.That(training.Decision!.Id, Is.EqualTo("1"));
    }

    [Test]
    public async Task Export_savesTheJudgedBacklogAndSaysHowMuchWent()
    {
        var api = new FakeTrainingApi { Export = "{\"id\":\"1\"}\n{\"id\":\"2\"}\n" };
        var sink = new FakeExportSink();
        var training = ViewModel(api, sink);

        await training.ExportCommand.Execute();

        Assert.Multiple(() =>
        {
            Assert.That(sink.FileName, Does.StartWith("agent-up-product-decisions-"));
            Assert.That(training.ExportMessage, Is.EqualTo("Exported 2 judged decisions."));
        });
    }

    [Test]
    public async Task Export_saysThereIsNothingToExportBeforeAnythingIsJudged()
    {
        var sink = new FakeExportSink();
        var training = ViewModel(new FakeTrainingApi(), sink);

        await training.ExportCommand.Execute();

        Assert.Multiple(() =>
        {
            Assert.That(training.ExportMessage, Does.Contain("Nothing to export"));
            Assert.That(sink.FileName, Is.Null);
        });
    }

    private static async Task Open(TrainingViewModel training)
        => await training.OpenRepositoryCommand.Execute(training.Repositories.Single());

    private static TrainingViewModel ViewModel(ITrainingApiProvider api, IDecisionExportSink? export = null)
        => new(new TrainingController(new TrainingService(api)), export);

    private static DecisionRepository Repository(string owner, string repository, int pending, int reviewed)
        => new(owner, repository, pending, reviewed,
        [
            new DecisionCategoryCount("product-vision", "Product vision", pending, reviewed),
            new DecisionCategoryCount("gui-decisions", "GUI", 0, 0),
            new DecisionCategoryCount("code-architecture", "Architecture", 0, 0)
        ]);

    private static ProductDecision Decision(
        string id,
        string category = "product-vision",
        string verdict = "pending",
        string? note = null)
        => new(id, "example", "repository", "ws-lucy", "Lucy", category,
            $"Headline {id}", $"Detail {id}", $"Evidence {id}", [],
            DateTimeOffset.UnixEpoch.AddMinutes(int.Parse(id)), verdict, note, null);

    private sealed class FakeTrainingApi : ITrainingApiProvider
    {
        public IReadOnlyList<DecisionRepository> Repositories { get; init; } = [];

        public IReadOnlyList<ProductDecision> Decisions { get; init; } = [];

        public string Export { get; init; } = string.Empty;

        public Exception? Failure { get; init; }

        public Exception? ReviewFailure { get; init; }

        public List<(string Id, string Verdict, string? Note)> Reviewed { get; } = [];

        public Task<IReadOnlyList<DecisionRepository>> ListRepositoriesAsync(CancellationToken cancellationToken)
            => Failure is null
                ? Task.FromResult(Repositories)
                : Task.FromException<IReadOnlyList<DecisionRepository>>(Failure);

        public Task<IReadOnlyList<ProductDecision>> ListDecisionsAsync(
            string owner,
            string repository,
            CancellationToken cancellationToken)
            => Task.FromResult(Decisions);

        public Task<DecisionEvidenceImage?> EvidenceAsync(string evidenceId, CancellationToken cancellationToken)
            => Task.FromResult<DecisionEvidenceImage?>(null);

        public Task ReviewAsync(string decisionId, string verdict, string? note, CancellationToken cancellationToken)
        {
            if (ReviewFailure is not null) return Task.FromException(ReviewFailure);
            Reviewed.Add((decisionId, verdict, note));
            return Task.CompletedTask;
        }

        public Task<string> ExportAsync(bool includePending, CancellationToken cancellationToken)
            => Task.FromResult(Export);
    }

    private sealed class FakeExportSink : IDecisionExportSink
    {
        public string? FileName { get; private set; }

        public string? Saved { get; private set; }

        public Task<bool> SaveAsync(string suggestedFileName, string jsonl, CancellationToken cancellationToken = default)
        {
            FileName = suggestedFileName;
            Saved = jsonl;
            return Task.FromResult(true);
        }
    }
}
