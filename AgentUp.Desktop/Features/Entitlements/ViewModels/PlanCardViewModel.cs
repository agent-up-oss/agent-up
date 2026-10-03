using System.Collections.ObjectModel;
using AgentUp.Desktop.Features.Entitlements.Models;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Entitlements.ViewModels;

public sealed class PlanCardViewModel : ReactiveObject
{
    private string _displayName = "";
    private string _billing = "";
    private string _summary = "";
    private bool _isVisible;

    public ObservableCollection<PlanCardFeature> Features { get; } = [];
    public ObservableCollection<PlanCardLimit> Limits { get; } = [];

    public string DisplayName
    {
        get => _displayName;
        private set => this.RaiseAndSetIfChanged(ref _displayName, value);
    }

    public string Billing
    {
        get => _billing;
        private set => this.RaiseAndSetIfChanged(ref _billing, value);
    }

    public string Summary
    {
        get => _summary;
        private set => this.RaiseAndSetIfChanged(ref _summary, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        private set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    public bool HasBilling => !string.IsNullOrWhiteSpace(Billing);

    public void Clear()
    {
        Apply(null);
    }

    public void Apply(PlanCard? card)
    {
        Features.Clear();
        Limits.Clear();
        if (card is null)
        {
            DisplayName = "";
            Billing = "";
            Summary = "";
            IsVisible = false;
            this.RaisePropertyChanged(nameof(HasBilling));
            return;
        }

        DisplayName = card.DisplayName;
        Billing = card.Billing;
        Summary = card.Summary;
        foreach (var feature in card.Features)
            Features.Add(feature);
        foreach (var limit in card.Limits)
            Limits.Add(limit);
        IsVisible = true;
        this.RaisePropertyChanged(nameof(HasBilling));
    }
}
