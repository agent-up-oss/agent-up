using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Features.Agents.Views;

namespace AgentUp.Desktop.Tests.Features.Agents.Headless;

[TestFixture]
public sealed class AgentRevealPresenterTests
{
    [AvaloniaTest]
    public void Measure_takesTheShareOfTheContentHeightItHasRevealed()
    {
        var presenter = Presenter(open: true);

        presenter.Reveal = 0.5;
        presenter.Measure(new Size(300, double.PositiveInfinity));

        Assert.Multiple(() =>
        {
            Assert.That(presenter.DesiredSize.Height, Is.EqualTo(60));
            Assert.That(presenter.Child!.Opacity, Is.EqualTo(0.5));
            Assert.That(presenter.ClipToBounds, Is.True, "content is revealed, not squashed");
        });
    }

    [AvaloniaTest]
    public void Closed_hidesTheContentAndTakesNoSpace()
    {
        var presenter = Presenter(open: true);

        presenter.IsOpen = false;
        presenter.Measure(new Size(300, double.PositiveInfinity));

        Assert.Multiple(() =>
        {
            Assert.That(presenter.Reveal, Is.Zero);
            Assert.That(presenter.Child!.IsVisible, Is.False);
            Assert.That(presenter.DesiredSize.Height, Is.Zero);
        });
    }

    [AvaloniaTest]
    public void Open_showsTheContentAtOnceWhileItsHeightGrows()
    {
        var presenter = Presenter(open: false);

        presenter.IsOpen = true;

        Assert.That(presenter.Child!.IsVisible, Is.True);
    }

    private static AgentRevealPresenter Presenter(bool open) =>
        new() { IsOpen = open, Child = new Border { Height = 120, Width = 200 } };
}
