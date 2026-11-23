using AgentUp.Desktop.DesignSystem;
using AgentUp.Desktop.Tests.Support;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;

namespace AgentUp.Desktop.Tests.Features.Interface.Headless;

/// <summary>
/// A scene control answers its own press once. Avalonia's Click bubbles, so a button inside a
/// card that has an action of its own (Play on a validation flow) must not also fire the card.
/// </summary>
[TestFixture]
public sealed class NewUiSceneActionTests
{
    [AvaloniaTest]
    public async Task PlayInsideAFlowCard_runsTheFlowOnce()
    {
        var (window, scene) = ApplicationScene();
        var actions = new List<string>();
        scene.ActionInvoked += (_, args) => actions.Add($"{args.Name} {args.Id}");
        window.Show();

        var play = ButtonWithClass(scene, "au-validation-play");
        await window.ClickControlAsync(play);

        Assert.That(actions, Is.EqualTo(new[] { "run checkout" }));
        window.Close();
    }

    [AvaloniaTest]
    public async Task LogoutAtTheFootOfTheRail_isOneAction()
    {
        var (window, scene) = ApplicationScene();
        var actions = new List<string>();
        scene.ActionInvoked += (_, args) => actions.Add(args.Name);
        window.Show();

        var logout = ButtonWithClass(scene, "au-session-logout");
        await window.ClickControlAsync(logout);

        Assert.That(actions, Is.EqualTo(new[] { "logout" }));
        window.Close();
    }

    // The board opens on Issues; choosing Dev review moves the selection there and shows that queue,
    // whose rows offer no button because reviewers take reviews themselves.
    [AvaloniaTest]
    public void TheTasksBoard_showsTheChosenReviewQueueUnderItsTab()
    {
        var scene = new NewUiScene();
        scene.Apply(
            "tasks",
            new Dictionary<string, string> { ["server"] = "Demo" },
            new Dictionary<string, IReadOnlyList<NewUiSlotItem>>
            {
                ["devReview"] =
                [
                    new NewUiSlotItem
                    {
                        Id = "I-39",
                        Action = "reviewing",
                        Text = { ["title"] = "#39 · Product cards cut off long names", ["agent"] = "Hector · revision 2", ["repo"] = "example/repository" },
                        Parts = { "agent" },
                    },
                ],
            },
            new Dictionary<string, string> { ["nav"] = "1", ["tasksTab"] = "dev", ["devTab"] = "1", ["repoOpen"] = "1" });
        var window = new Window { Width = 1440, Height = 900, Content = scene };
        window.Show();

        var tabs = scene.GetVisualDescendants().OfType<Button>()
            .Where(button => Words(button) is "Issues" or "Dev review" or "PM review")
            .ToDictionary(Words, button => button.Classes.Contains("au-app-tab--selected") || button.Content is Border { } face && face.Classes.Contains("au-app-tab--selected"));
        var words = scene.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(tabs, Is.EqualTo(new Dictionary<string, bool> { ["Issues"] = false, ["Dev review"] = true, ["PM review"] = false }));
            Assert.That(words, Does.Contain("Hector · revision 2").And.Contain("Under review · 1"));
            Assert.That(words, Does.Not.Contain("Dequeue").And.Not.Contain("Stop"));
        });
        window.Close();
    }

    private static string Words(Button button) =>
        string.Join(" ", button.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text)).Trim();

    /// <summary>A scene button carries its class itself or on the face it paints.</summary>
    private static Button ButtonWithClass(NewUiScene scene, string name) =>
        scene.GetVisualDescendants().OfType<Button>().First(button =>
            button.Classes.Contains(name) || button.Content is Border { } face && face.Classes.Contains(name));

    private static (Window Window, NewUiScene Scene) ApplicationScene()
    {
        var scene = new NewUiScene();
        scene.Apply(
            "application",
            new Dictionary<string, string> { ["server"] = "Demo" },
            new Dictionary<string, IReadOnlyList<NewUiSlotItem>>
            {
                ["flows"] =
                [
                    new NewUiSlotItem
                    {
                        Id = "checkout",
                        Text = { ["name"] = "Checkout featured goods", ["description"] = "Order a Harbor Mug." },
                        Parts = { "play" },
                    },
                ],
            },
            new Dictionary<string, string> { ["checks"] = "1", ["expanded"] = "1" });
        return (new Window { Width = 1440, Height = 900, Content = scene }, scene);
    }
}
