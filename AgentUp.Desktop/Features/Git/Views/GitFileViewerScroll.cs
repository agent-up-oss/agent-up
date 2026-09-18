using System.Collections;
using Avalonia;
using Avalonia.Controls;

namespace AgentUp.Desktop.Features.Git.Views;

public static class GitFileViewerScroll
{
    public static readonly AttachedProperty<int> TargetIndexProperty =
        AvaloniaProperty.RegisterAttached<ListBox, int>("TargetIndex", typeof(GitFileViewerScroll), -1);

    public static int GetTargetIndex(AvaloniaObject element) => element.GetValue(TargetIndexProperty);

    public static void SetTargetIndex(AvaloniaObject element, int value) => element.SetValue(TargetIndexProperty, value);

    static GitFileViewerScroll()
    {
        TargetIndexProperty.Changed.AddClassHandler<ListBox>((list, args) =>
        {
            if (args.NewValue is not int index || index < 0)
                return;
            if (list.ItemsSource is not IList items || index >= items.Count)
                return;
            list.ScrollIntoView(items[index]!);
        });
    }
}
