using AgentUp.Browser.Streaming.Tests.Fake;

namespace AgentUp.Browser.Streaming.Tests.Features.Streaming.Unit;

[TestFixture]
public sealed class WorkspaceSubscriberSetTests
{
    [Test]
    public void IsEmpty_isTrueForANewSet()
    {
        Assert.That(new WorkspaceSubscriberSet().IsEmpty, Is.True);
    }

    [Test]
    public void Add_thenRemove_returnsTheSetToEmpty()
    {
        var set = new WorkspaceSubscriberSet();
        var id = Guid.NewGuid();

        set.Add(id, new FakeSubscriberConnection());
        var populated = set.IsEmpty;
        set.Remove(id);

        Assert.Multiple(() =>
        {
            Assert.That(populated, Is.False);
            Assert.That(set.IsEmpty, Is.True);
        });
    }

    [Test]
    public void Remove_ignoresAnUnknownSubscriber()
    {
        var set = new WorkspaceSubscriberSet();
        set.Add(Guid.NewGuid(), new FakeSubscriberConnection());

        set.Remove(Guid.NewGuid());

        Assert.That(set.IsEmpty, Is.False);
    }

    [Test]
    public void Add_replacesAnExistingEntryForTheSameId()
    {
        var set = new WorkspaceSubscriberSet();
        var id = Guid.NewGuid();
        set.Add(id, new FakeSubscriberConnection());
        set.SetPresence(id, PresenceState.Background);

        set.Add(id, new FakeSubscriberConnection());

        Assert.That(set.HasForeground(), Is.True, "A re-added subscriber starts in the foreground again.");
    }

    [Test]
    public void SubscribersStartInTheForeground()
    {
        var set = new WorkspaceSubscriberSet();
        set.Add(Guid.NewGuid(), new FakeSubscriberConnection());

        Assert.That(set.HasForeground(), Is.True);
    }

    [Test]
    public void SetPresence_reportsWhetherThePresenceActuallyChanged()
    {
        var set = new WorkspaceSubscriberSet();
        var id = Guid.NewGuid();
        set.Add(id, new FakeSubscriberConnection());

        var changed = set.SetPresence(id, PresenceState.Background);
        var unchanged = set.SetPresence(id, PresenceState.Background);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(unchanged, Is.False, "Re-asserting the same presence is not a change.");
        });
    }

    [Test]
    public void SetPresence_returnsFalseForAnUnknownSubscriber()
    {
        Assert.That(new WorkspaceSubscriberSet().SetPresence(Guid.NewGuid(), PresenceState.Background), Is.False);
    }

    [Test]
    public void HasForeground_isFalseWhenEverySubscriberIsBackgrounded()
    {
        var set = new WorkspaceSubscriberSet();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        set.Add(first, new FakeSubscriberConnection());
        set.Add(second, new FakeSubscriberConnection());

        set.SetPresence(first, PresenceState.Background);
        set.SetPresence(second, PresenceState.Background);

        Assert.That(set.HasForeground(), Is.False);
    }

    [Test]
    public void HasForeground_isTrueWhileAnySubscriberIsForegrounded()
    {
        var set = new WorkspaceSubscriberSet();
        var backgrounded = Guid.NewGuid();
        set.Add(backgrounded, new FakeSubscriberConnection());
        set.Add(Guid.NewGuid(), new FakeSubscriberConnection());

        set.SetPresence(backgrounded, PresenceState.Background);

        Assert.That(set.HasForeground(), Is.True);
    }

    [Test]
    public void HasForeground_isFalseForAnEmptySet()
    {
        Assert.That(new WorkspaceSubscriberSet().HasForeground(), Is.False);
    }
}
