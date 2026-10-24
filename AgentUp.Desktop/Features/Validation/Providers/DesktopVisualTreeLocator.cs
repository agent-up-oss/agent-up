using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using AgentUp.Desktop.Features.Validation.DTOs;

namespace AgentUp.Desktop.Features.Validation.Providers;

public sealed class DesktopVisualTreeLocator
{
    public Control? Resolve(Visual root, ValidationTargetDto target, out IReadOnlyList<DesktopTargetCandidateDto> candidates)
    {
        var controls = root.GetVisualDescendants().OfType<Control>().ToList();
        candidates = controls
            .Select(control => new DesktopTargetCandidateDto(
                control.GetType().Name,
                AutomationId(control),
                control.Name))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.AutomationId) || !string.IsNullOrWhiteSpace(candidate.Name))
            .Take(20)
            .ToList();

        var matches = controls.Where(control => Matches(control, target)).ToList();
        return matches.FirstOrDefault(IsShown) ?? matches.FirstOrDefault();
    }

    public DesktopResolvedTargetDto Describe(Control control)
    {
        var bounds = control.Bounds;
        return new DesktopResolvedTargetDto(
            control.GetType().Name,
            AutomationId(control),
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height);
    }

    public IReadOnlyList<DesktopTargetCandidateDto> Nearby(Visual root, ValidationTargetDto target)
    {
        var wanted = target.TestId ?? target.Name ?? target.Label ?? target.Text ?? target.Role ?? "";
        return root.GetVisualDescendants().OfType<Control>()
            .Select(control => new DesktopTargetCandidateDto(
                control.GetType().Name,
                AutomationId(control),
                control.Name))
            .Where(candidate =>
                Contains(candidate.AutomationId, wanted)
                || Contains(candidate.Name, wanted)
                || Contains(candidate.ControlType, wanted))
            .Take(12)
            .ToList();
    }

    private static bool Matches(Control control, ValidationTargetDto target)
    {
        if (HasText(target.TestId) && !string.Equals(AutomationId(control), target.TestId, StringComparison.Ordinal))
            return false;
        if (HasText(target.Name) && !string.Equals(control.Name, target.Name, StringComparison.Ordinal)
            && !string.Equals(AutomationProperties.GetName(control), target.Name, StringComparison.Ordinal))
            return false;
        if (HasText(target.Label) && !Contains(AccessibleText(control), target.Label))
            return false;
        if (HasText(target.Role) && !string.Equals(control.GetType().Name, target.Role, StringComparison.OrdinalIgnoreCase))
            return false;
        if (HasText(target.Text) && !Contains(AccessibleText(control), target.Text))
            return false;
        return HasText(target.TestId) || HasText(target.Name) || HasText(target.Label) || HasText(target.Role) || HasText(target.Text);
    }

    private static bool IsShown(Control control) => control.IsEffectivelyVisible && control.IsAttachedToVisualTree();

    private static string? AutomationId(Control control)
    {
        var id = AutomationProperties.GetAutomationId(control);
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }

    private static string AccessibleText(Control control)
    {
        if (control is ContentControl { Content: string content })
            return content;
        if (control is TextBox textBox)
            return textBox.Text ?? "";
        if (control is TextBlock textBlock)
            return textBlock.Text ?? "";
        return AutomationProperties.GetName(control) ?? control.Name ?? "";
    }

    private static bool Contains(string? haystack, string? needle) =>
        HasText(haystack) && HasText(needle) && haystack!.Contains(needle!, StringComparison.OrdinalIgnoreCase);

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);
}
