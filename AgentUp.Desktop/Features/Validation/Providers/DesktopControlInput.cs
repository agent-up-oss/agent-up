using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace AgentUp.Desktop.Features.Validation.Providers;

public sealed class DesktopControlInput
{
    private static int _pointerId = 1000;
    private static readonly MethodInfo? ButtonOnClick = typeof(Button).GetMethod(
        "OnClick",
        BindingFlags.Instance | BindingFlags.NonPublic);

    public void Click(Control control)
    {
        if (!control.IsEffectivelyEnabled || !control.IsEffectivelyVisible)
            return;

        Focus(control);
        if (control is Button button && ButtonOnClick is not null)
        {
            ButtonOnClick.Invoke(button, null);
            return;
        }

        if (TrySelectListBoxItem(control))
            return;

        RaisePointer(control);
    }

    private static bool TrySelectListBoxItem(Control control)
    {
        var item = control as ListBoxItem
                   ?? control.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
        if (item is null)
            return false;
        var list = item.GetVisualAncestors().OfType<ListBox>().FirstOrDefault();
        if (list is null)
            return false;
        list.SelectedItem = item.DataContext ?? item;
        return true;
    }

    public void Fill(Control control, string value)
    {
        Focus(control);
        if (control is TextBox textBox)
        {
            textBox.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.A,
                KeyModifiers = KeyModifiers.Control
            });
            var input = new TextInputEventArgs
            {
                RoutedEvent = InputElement.TextInputEvent,
                Text = value
            };
            textBox.RaiseEvent(input);
            if (string.IsNullOrEmpty(textBox.Text) || !string.Equals(textBox.Text, value, StringComparison.Ordinal))
                textBox.Text = value;
            return;
        }

        control.RaiseEvent(new TextInputEventArgs
        {
            RoutedEvent = InputElement.TextInputEvent,
            Text = value
        });
    }

    public void Press(Control? control, string key)
    {
        var target = control ?? FocusManagerTarget();
        if (target is null)
            return;

        Focus(target);
        var parsed = ParseKey(key);
        target.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = parsed
        });
        target.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyUpEvent,
            Key = parsed
        });
    }

    private static void RaisePointer(Control control)
    {
        using var pointer = new Avalonia.Input.Pointer(Interlocked.Increment(ref _pointerId), PointerType.Mouse, true);
        var center = Center(control);
        var pressed = new PointerPressedEventArgs(
            control,
            pointer,
            control,
            center,
            0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None);
        control.RaiseEvent(pressed);

        var released = new PointerReleasedEventArgs(
            control,
            pointer,
            control,
            center,
            0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None,
            MouseButton.Left);
        control.RaiseEvent(released);
    }

    private static void Focus(Control control)
    {
        if (control.Focusable)
            control.Focus();
    }

    private static Point Center(Control control) =>
        new(Math.Max(1, control.Bounds.Width / 2), Math.Max(1, control.Bounds.Height / 2));

    private static Control? FocusManagerTarget() =>
        TopLevel.GetTopLevel(null)?.FocusManager?.GetFocusedElement() as Control;

    private static Key ParseKey(string key) =>
        Enum.TryParse<Key>(key, ignoreCase: true, out var parsed) ? parsed : Key.Enter;
}
