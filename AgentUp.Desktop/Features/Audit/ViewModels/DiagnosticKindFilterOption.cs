using ReactiveUI;

namespace AgentUp.Desktop.Features.Audit.ViewModels;

public sealed class DiagnosticKindFilterOption : ReactiveObject
{
    public DiagnosticKindFilterOption(string label, string kind, bool isSelected = true, string? stream = null)
    {
        Label = label;
        Kind = kind;
        Stream = stream;
        _isSelected = isSelected;
    }

    public string Label { get; }
    public string Kind { get; }
    public string? Stream { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }
}
