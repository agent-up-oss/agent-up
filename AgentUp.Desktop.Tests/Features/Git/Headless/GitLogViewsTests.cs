using System.Collections;
using System.Globalization;
using AgentUp.Desktop.Features.Git.DTOs;
using AgentUp.Desktop.Features.Git.Providers;
using AgentUp.Desktop.Features.Git.Views;
using AgentUp.Desktop.Tests.Support;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;

namespace AgentUp.Desktop.Tests.Features.Git.Headless;

[TestFixture]
public sealed class GitLogViewsTests
{
    [AvaloniaTest]
    public async Task GitLogGraphColumn_measuresAndRendersIncomingOutgoingAndHead()
    {
        var app = await AppDriver.LaunchEmptyAsync();
        var merge = DesktopDomain.GitCommit().WithId("ccc").WithParents("bbb", "aaa").Saying("merge").At("2026-01-03T00:00:00Z").WithRefs("HEAD", "main").Build();
        var child = DesktopDomain.GitCommit().WithId("bbb").WithParents("aaa").Saying("child").At("2026-01-02T00:00:00Z").Build();
        var parent = DesktopDomain.GitCommit().WithId("aaa").Saying("root").At("2026-01-01T00:00:00Z").Build();
        var rows = GitLogLayoutProvider.Layout([merge, child, parent]);
        var column = new GitLogGraphColumn
        {
            Width = 80,
            Height = GitLogLayoutProvider.RowHeight,
            Row = rows[0]
        };
        app.Window.Content = column;
        await HeadlessExtensions.FlushAsync();

        Assert.That(column.Bounds.Width, Is.GreaterThan(1));
        Assert.That(column.Row!.IncomingLanes, Is.Empty);
        Assert.That(column.Row.Outgoing.Any(link => link.FromLane != link.ToLane), Is.True);

        column.Row = null;
        await HeadlessExtensions.FlushAsync();
        Assert.That(column.Row, Is.Null);

        column.Row = rows[1];
        await HeadlessExtensions.FlushAsync();
        Assert.That(column.Row!.IncomingLanes, Is.Not.Empty);
    }

    [AvaloniaTest]
    public async Task GitFileViewerScroll_scrollsToAValidIndex()
    {
        var app = await AppDriver.LaunchEmptyAsync();
        var list = new ListBox
        {
            Width = 240,
            Height = 120,
            ItemsSource = new[] { "a", "b", "c" }
        };
        app.Window.Content = list;
        await HeadlessExtensions.FlushAsync();

        GitFileViewerScroll.SetTargetIndex(list, 2);
        Assert.That(GitFileViewerScroll.GetTargetIndex(list), Is.EqualTo(2));
        GitFileViewerScroll.SetTargetIndex(list, -1);
        GitFileViewerScroll.SetTargetIndex(list, 99);
        Assert.That(((IList)list.ItemsSource!).Count, Is.EqualTo(3));
    }

    [Test]
    public void GitLogTimeConverter_formatsTimestampsAndRejectsConvertBack()
    {
        Assert.That(
            GitLogTimeConverter.Instance.Convert("not-a-date", typeof(string), null, CultureInfo.InvariantCulture),
            Is.EqualTo("not-a-date"));
        Assert.That(
            GitLogTimeConverter.Instance.Convert(null, typeof(string), null, CultureInfo.InvariantCulture),
            Is.EqualTo(string.Empty));
        Assert.That(
            () => GitLogTimeConverter.Instance.ConvertBack("just now", typeof(string), null, CultureInfo.InvariantCulture),
            Throws.TypeOf<NotSupportedException>());
    }
}
