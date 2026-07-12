using System.Collections.ObjectModel;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Applications.ViewModels;

public sealed class ApplicationListViewModel : ReactiveObject
{
    private ApplicationViewModel? _selectedApplication;

    public ObservableCollection<ApplicationViewModel> Applications { get; } = [];

    public ApplicationViewModel? SelectedApplication
    {
        get => _selectedApplication;
        set => this.RaiseAndSetIfChanged(ref _selectedApplication, value);
    }

    public void Update(IReadOnlyList<ApplicationViewModel> applications)
    {
        Applications.Clear();
        foreach (var app in applications)
            Applications.Add(app);
        SelectedApplication = Applications.FirstOrDefault();
    }
}
