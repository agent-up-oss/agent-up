using System.Collections.ObjectModel;
using AgentUp.Desktop.Features.Database.Controllers;
using AgentUp.Desktop.Features.Database.DTOs;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Database.ViewModels;

public sealed class DatabaseViewModel : ReactiveObject
{
    private readonly DatabaseController _controller;
    private string? _workspaceId;
    private string? _applicationName;
    private DatabaseDto? _selectedDatabase;
    private DatabaseTableDto? _selectedTable;
    private string _sql = string.Empty;
    private string? _error;
    private string _status = string.Empty;

    public ObservableCollection<DatabaseDto> Databases { get; } = [];
    public ObservableCollection<DatabaseTableDto> Tables { get; } = [];
    public ObservableCollection<string> Columns { get; } = [];
    public ObservableCollection<string> Rows { get; } = [];
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> ExecuteCommand { get; }

    public DatabaseDto? SelectedDatabase
    {
        get => _selectedDatabase;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedDatabase, value);
            SelectDatabase(value);
        }
    }

    public DatabaseTableDto? SelectedTable
    {
        get => _selectedTable;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTable, value);
            if (value is not null)
                Sql = $"SELECT * FROM \"{value.Schema.Replace("\"", "\"\"")}\".\"{value.Name.Replace("\"", "\"\"")}\" LIMIT 50;";
        }
    }
    public string Sql { get => _sql; set => this.RaiseAndSetIfChanged(ref _sql, value); }
    public string? Error { get => _error; private set { this.RaiseAndSetIfChanged(ref _error, value); this.RaisePropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public string Status { get => _status; private set => this.RaiseAndSetIfChanged(ref _status, value); }

    public DatabaseViewModel(DatabaseController controller)
    {
        _controller = controller;
        ExecuteCommand = ReactiveCommand.CreateFromTask(ExecuteAsync);
    }

    public async Task LoadAsync(string workspaceId, string applicationName)
    {
        _workspaceId = workspaceId;
        _applicationName = applicationName;
        Error = null;
        try
        {
            var catalog = await _controller.GetCatalogAsync(workspaceId, applicationName, CancellationToken.None);
            Databases.Clear();
            foreach (var database in catalog.Databases) Databases.Add(database);
            SelectedDatabase = Databases.FirstOrDefault();
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            Error = ex.Message;
        }
    }

    private void SelectDatabase(DatabaseDto? database)
    {
        Tables.Clear();
        foreach (var table in database?.Tables ?? []) Tables.Add(table);
        SelectedTable = Tables.FirstOrDefault();
    }

    private async Task ExecuteAsync()
    {
        if (_workspaceId is null || _applicationName is null || SelectedDatabase is null || string.IsNullOrWhiteSpace(Sql)) return;
        Error = null;
        try
        {
            var result = await _controller.ExecuteAsync(_workspaceId, _applicationName, SelectedDatabase.Name, Sql, CancellationToken.None);
            Columns.Clear(); Rows.Clear();
            foreach (var column in result.Columns) Columns.Add(column);
            foreach (var row in result.Rows) Rows.Add(string.Join("  |  ", row.Select(value => value ?? "NULL")));
            Status = result.Columns.Count > 0 ? $"{result.Rows.Count} row(s)" : $"{result.AffectedRows} row(s) affected";
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            Error = ex.Message;
        }
    }
}
