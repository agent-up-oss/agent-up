using System.Reactive.Linq;
using AgentUp.Desktop.Features.Interface.Controllers;
using AgentUp.Desktop.Features.Interface.Interfaces;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Services;
using AgentUp.Desktop.Features.Interface.ViewModels;
using Avalonia.Headless.NUnit;

namespace AgentUp.Desktop.Tests.Features.Interface.Headless;

/// <summary>
/// The picture behind a decision.
///
/// It is decoded from bytes the Server handed over on the same authenticated read as the rest of
/// the backlog, which is why this needs a real Avalonia platform rather than a plain unit test: a
/// picture the view could fetch by URL on its own would carry no credentials and would miss the
/// Demo's intercepted HttpClient entirely.
/// </summary>
[TestFixture]
public sealed class DecisionEvidencePictureTests
{
    // Garbage bytes are not covered here: the headless platform's bitmap accepts anything, so a
    // test for them would assert the stub rather than the product. The decode throws
    // ArgumentException on a real platform, which the view model already catches.
    private const string EvidencePng =
        "iVBORw0KGgoAAAANSUhEUgAAAUAAAAC0CAIAAABqhmJGAAABp0lEQVR42u3dUQkAIBBEwcsgYhvbGdMwgoaQhYFJsPC+t1ofQKgyAQgYEDAgYBAwIGBAwICAQcCAgAEBg4ABAQMCBgQMAgYEDAgYEDAIGBAwIGAQMCBgQMCAgEHAgIABAQM34LkXEErAIGBAwICAQcCAgAEBAwIGAQMCBgQMAgYEDAgYEDAIGBAwIGBAwCBgQMCAgEHAgIABAQMCBgEDAgYEDLyAHTSCf2BAwICAQcCAgAEBAwIGAQMCBgQMAgYEDAgYEDAIGBAwIGBAwCBgQMCAgEHAgIABAQMCBgEDAgYEDAgY0gN20Aj+gQEBAwIGAQMCBgQMCBgEDAgYEDAIGBAwIGBAwCBgQMCAgAEBg4ABAQMCBgEDAgYEDAgYBAwIGBAwIGBID9hBI/gHBgQMCBgEDAgYEDAgYBAwIGBAwCBgQMCAgAEBg4ABAQMCBgQMAgYEDAgYBAwIGBAwIGAQMCBgQMCAgEHAgIABAYOAAQEDAgYEDAIGBAwIGARsBRAwIGBAwCBgQMCAgAEBg4ABAQMCBgEDAgYEDAgYBAwIGBAwIGAQMPDVAegeMCXag64RAAAAAElFTkSuQmCC";

    [AvaloniaTest]
    public async Task A_decision_with_a_picture_draws_it_from_the_bytes_the_Server_sent()
    {
        var api = new PictureApi { Image = new DecisionEvidenceImage("ev-1", "image/png", EvidencePng) };
        var training = await OpenedAsync(api, Pictured());

        Assert.Multiple(() =>
        {
            Assert.That(training.HasEvidencePicture, Is.True);
            Assert.That(training.EvidencePicture, Is.Not.Null);
            Assert.That(api.Asked, Is.EqualTo(new[] { "ev-1" }));
        });
    }

    [AvaloniaTest]
    public async Task A_decision_with_no_picture_asks_for_none()
    {
        var api = new PictureApi();
        var training = await OpenedAsync(api, Plain());

        Assert.Multiple(() =>
        {
            Assert.That(training.HasEvidencePicture, Is.False);
            Assert.That(api.Asked, Is.Empty);
        });
    }

    [AvaloniaTest]
    public async Task A_picture_the_backlog_no_longer_holds_leaves_the_words_to_carry_the_card()
    {
        var api = new PictureApi();
        var training = await OpenedAsync(api, Pictured());

        Assert.Multiple(() =>
        {
            Assert.That(training.HasEvidencePicture, Is.False);
            Assert.That(training.Headline, Is.Not.Empty, "the decision still reads");
            Assert.That(training.HasError, Is.False, "a missing picture is not an error line");
        });
    }

    [AvaloniaTest]
    public async Task A_read_that_fails_leaves_the_card_without_a_picture_rather_than_throwing()
    {
        var api = new PictureApi { Failure = new HttpRequestException("picture refused") };
        var training = await OpenedAsync(api, Pictured());

        Assert.Multiple(() =>
        {
            Assert.That(training.HasEvidencePicture, Is.False);
            Assert.That(training.HasError, Is.False, "the words still carry the card");
        });
    }

    [AvaloniaTest]
    public async Task Moving_from_a_pictured_decision_to_one_without_clears_the_picture()
    {
        var api = new PictureApi { Image = new DecisionEvidenceImage("ev-1", "image/png", EvidencePng) };
        api.Decisions = [Pictured(), Plain() with { Id = "pd-2" }];
        var training = new TrainingViewModel(new TrainingController(new TrainingService(api)));
        await training.LoadAsync();
        await training.OpenRepositoryCommand.Execute(training.Repositories.Single());

        Assert.That(training.HasEvidencePicture, Is.True, "the first decision has one");

        training.NextCommand.Execute().Subscribe();
        await Task.Yield();

        Assert.Multiple(() =>
        {
            Assert.That(training.HasEvidencePicture, Is.False);
            Assert.That(training.Decision!.Id, Is.EqualTo("pd-2"));
        });
    }

    private static async Task<TrainingViewModel> OpenedAsync(PictureApi api, ProductDecision decision)
    {
        api.Decisions = [decision];
        var training = new TrainingViewModel(new TrainingController(new TrainingService(api)));
        await training.LoadAsync();
        await training.OpenRepositoryCommand.Execute(training.Repositories.Single());
        return training;
    }

    private static ProductDecision Pictured()
        => Plain() with { EvidenceImages = [new DecisionEvidence("ev-1", "artifact-1", "image/png")] };

    private static ProductDecision Plain()
        => new("pd-1", "example", "repository", "ws-lucy", "Lucy", "product-vision",
            "Headline", "Detail", "Evidence", [], DateTimeOffset.UnixEpoch, "pending", null, null);

    private sealed class PictureApi : ITrainingApiProvider
    {
        public IReadOnlyList<ProductDecision> Decisions { get; set; } = [];

        public DecisionEvidenceImage? Image { get; init; }

        public Exception? Failure { get; init; }

        public List<string> Asked { get; } = [];

        public Task<IReadOnlyList<DecisionRepository>> ListRepositoriesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DecisionRepository>>(
            [
                new DecisionRepository("example", "repository", 1, 0,
                [
                    new DecisionCategoryCount("product-vision", "Product vision", 1, 0)
                ])
            ]);

        public Task<IReadOnlyList<ProductDecision>> ListDecisionsAsync(
            string owner,
            string repository,
            CancellationToken cancellationToken)
            => Task.FromResult(Decisions);

        public Task<DecisionEvidenceImage?> EvidenceAsync(string evidenceId, CancellationToken cancellationToken)
        {
            Asked.Add(evidenceId);
            return Failure is null
                ? Task.FromResult(Image)
                : Task.FromException<DecisionEvidenceImage?>(Failure);
        }

        public Task ReviewAsync(string decisionId, string verdict, string? note, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<string> ExportAsync(bool includePending, CancellationToken cancellationToken)
            => Task.FromResult(string.Empty);
    }
}
