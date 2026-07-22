using System.Collections.ObjectModel;
using AgentUp.Desktop.Features.Applications.Controllers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Applications.ViewModels;

public sealed class ApplicationListViewModel : ReactiveObject
{
    private readonly ApplicationsController _applications;
    private ApplicationViewModel? _selectedApplication;

    public ObservableCollection<ApplicationViewModel> Applications { get; } = [];

    public ApplicationListViewModel(ApplicationsController applications)
    {
        _applications = applications;
    }

    public ApplicationViewModel? SelectedApplication
    {
        get => _selectedApplication;
        set => this.RaiseAndSetIfChanged(ref _selectedApplication, value);
    }

    public void Update(IReadOnlyList<ApplicationViewModel> applications)
    {
        Applications.Clear();
        foreach (var app in _applications.Normalize(applications))
            Applications.Add(app);
        SelectedApplication = Applications.FirstOrDefault();
    }
}
