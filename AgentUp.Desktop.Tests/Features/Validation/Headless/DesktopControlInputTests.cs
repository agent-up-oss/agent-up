using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using AgentUp.Desktop.Features.Validation.Providers;

namespace AgentUp.Desktop.Tests.Features.Validation.Headless;

public sealed class DesktopControlInputTests
{
    [AvaloniaTest]
    public void Click_doesNotRaiseOnADisabledButton()
    {
        var clicked = false;
        var button = new Button { Content = "Go", IsEnabled = false };
        button.Click += (_, _) => clicked = true;
        Show(button);

        new DesktopControlInput().Click(button);

        Assert.That(clicked, Is.False);
    }

    [AvaloniaTest]
    public void Click_doesNotRaiseOnAHiddenButton()
    {
        var clicked = false;
        var button = new Button { Content = "Go", IsVisible = false };
        button.Click += (_, _) => clicked = true;
        Show(button);

        new DesktopControlInput().Click(button);

        Assert.That(clicked, Is.False);
    }

    [AvaloniaTest]
    public void Click_raisesOnClickForAnEnabledButton()
    {
        var clicked = false;
        var button = new Button { Content = "Go" };
        button.Click += (_, _) => clicked = true;
        Show(button);

        new DesktopControlInput().Click(button);

        Assert.That(clicked, Is.True);
    }

    [AvaloniaTest]
    public void Fill_appliesTextInputOnATextBox()
    {
        var box = new TextBox { Text = "old" };
        Show(box);

        new DesktopControlInput().Fill(box, "feat(Git): cover");

        Assert.That(box.Text, Is.EqualTo("feat(Git): cover"));
    }

    [AvaloniaTest]
    public void Click_selectsTheContainingListBoxItem()
    {
        var list = new ListBox
        {
            Width = 280,
            Height = 160,
            ItemsSource = new[] { "Apps", "Git", "Agent" }
        };
        Show(list);
        var item = list.GetVisualDescendants().OfType<ListBoxItem>().Skip(1).First();

        new DesktopControlInput().Click(item);

        Assert.That(list.SelectedItem, Is.EqualTo("Git"));
    }

    [AvaloniaTest]
    public void Click_togglesACheckBoxThroughPointerEvents()
    {
        var box = new CheckBox { Content = "On" };
        Show(box);

        new DesktopControlInput().Click(box);

        Assert.That(box.IsChecked, Is.True);
    }

    [AvaloniaTest]
    public void Fill_assignsTextWhenTextInputDoesNotApply()
    {
        var box = new IgnoreInputBox();
        Show(box);

        new DesktopControlInput().Fill(box, "feat(Git): cover");

        Assert.That(box.Text, Is.EqualTo("feat(Git): cover"));
    }

    [AvaloniaTest]
    public void Fill_raisesTextInputOnANonTextBox()
    {
        string? typed = null;
        var button = new Button { Content = "Go" };
        button.TextInput += (_, args) => typed = args.Text;
        Show(button);

        new DesktopControlInput().Fill(button, "hello");

        Assert.That(typed, Is.EqualTo("hello"));
    }

    [AvaloniaTest]
    public void Press_sendsTheNamedKeyAndFallsBackToEnter()
    {
        Key? down = null;
        var box = new TextBox();
        box.KeyDown += (_, args) => down = args.Key;
        Show(box);

        new DesktopControlInput().Press(box, "Escape");
        Assert.That(down, Is.EqualTo(Key.Escape));

        new DesktopControlInput().Press(box, "not-a-key");
        Assert.That(down, Is.EqualTo(Key.Enter));
    }

    [AvaloniaTest]
    public void Press_noOpsWhenNoControlIsFocused()
    {
        Assert.DoesNotThrow(() => new DesktopControlInput().Press(null, "Enter"));
    }

    private static void Show(Control content)
    {
        var window = new Window { Width = 320, Height = 180, Content = content };
        window.Show();
    }

    private sealed class IgnoreInputBox : TextBox
    {
        protected override void OnTextInput(TextInputEventArgs e)
        {
        }
    }
}
