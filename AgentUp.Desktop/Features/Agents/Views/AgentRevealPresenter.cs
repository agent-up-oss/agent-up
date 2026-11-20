using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using AgentUp.Desktop.Shared.Providers;

namespace AgentUp.Desktop.Features.Agents.Views;

/// <summary>
/// Opens and closes its content by growing and shrinking its own height while the content fades,
/// so what sits below glides instead of jumping. Closed, the content is not measured at all, so a
/// folded section costs nothing. With motion off it opens and closes at once.
/// </summary>
public sealed class AgentRevealPresenter : Decorator
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<AgentRevealPresenter, bool>(nameof(IsOpen));

    /// <summary>How far open it is drawn, from 0 to 1. Animated towards <see cref="IsOpen"/>.</summary>
    public static readonly StyledProperty<double> RevealProperty =
        AvaloniaProperty.Register<AgentRevealPresenter, double>(nameof(Reveal));

    static AgentRevealPresenter()
    {
        AffectsMeasure<AgentRevealPresenter>(RevealProperty);
    }

    public AgentRevealPresenter()
    {
        ClipToBounds = true;
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public double Reveal
    {
        get => GetValue(RevealProperty);
        set => SetValue(RevealProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Whatever state it is first drawn in is shown as it is; only later changes move.
        Reveal = IsOpen ? 1 : 0;
        Transitions = MotionProvider.Enabled
            ? [new DoubleTransition { Property = RevealProperty, Duration = MotionProvider.Reveal, Easing = new CubicEaseOut() }]
            : null;
        Apply();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
            Reveal = IsOpen ? 1 : 0;
        if (change.Property == IsOpenProperty || change.Property == RevealProperty || change.Property == ChildProperty)
            Apply();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { IsVisible: true } child)
            return default;
        child.Measure(availableSize);
        return new Size(child.DesiredSize.Width, child.DesiredSize.Height * Math.Clamp(Reveal, 0, 1));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // The content keeps its full height and is clipped, so it is revealed rather than squashed.
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Child.DesiredSize.Height));
        return finalSize;
    }

    private void Apply()
    {
        if (Child is null)
            return;
        Child.IsVisible = IsOpen || Reveal > 0.001;
        Child.Opacity = Math.Clamp(Reveal, 0, 1);
    }
}
