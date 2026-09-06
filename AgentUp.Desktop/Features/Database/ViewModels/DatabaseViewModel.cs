using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;
using AgentUp.Desktop.Features.Database.Controllers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Database.ViewModels;

public sealed class DatabaseViewModel : ReactiveObject
{
    private readonly DatabaseController _database;
    private CancellationTokenSource? _activeLoad;
    private long _loadVersion;
    private string? _workspaceId;
    private string? _applicationName;
    private string? _selectedDatabase;
    private string? _selectedTable;
    private string _sqlQuery = string.Empty;
    private bool _isLoading;
    private string? _errorMessage;

    public ObservableCollection<string> Databases { get; } = [];
    public ObservableCollection<string> Tables { get; } = [];
    public ObservableCollection<string> Columns { get; } = [];
    public ObservableCollection<DatabaseRowViewModel> Rows { get; } = [];

    public string? SelectedDatabase
    {
        get => _selectedDatabase;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedDatabase, value);
            if (value is not null)
                _ = LoadTablesAsync(value);
        }
    }

    public string? SelectedTable
    {
        get => _selectedTable;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTable, value);
            if (value is not null && _selectedDatabase is not null)
            {
                SqlQuery = BuildDefaultQuery(value);
                _ = RunQueryAsync();
            }
        }
    }

    public string SqlQuery
    {
        get => _sqlQuery;
        set => this.RaiseAndSetIfChanged(ref _sqlQuery, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isLoading, value);
            this.RaisePropertyChanged(nameof(ShowEmptyState));
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            this.RaiseAndSetIfChanged(ref _errorMessage, value);
            this.RaisePropertyChanged(nameof(ShowEmptyState));
        }
    }

    public bool HasResults => Columns.Count > 0;
    public bool ShowEmptyState => !IsLoading && !HasResults && string.IsNullOrWhiteSpace(ErrorMessage);

    public ReactiveCommand<Unit, Unit> RunQueryCommand { get; }

    public DatabaseViewModel(DatabaseController database)
    {
        _database = database;
        RunQueryCommand = ReactiveCommand.CreateFromTask(RunQueryAsync);
    }

    public void Clear()
    {
        Databases.Clear();
        Tables.Clear();
        Columns.Clear();
        Rows.Clear();
        _selectedDatabase = null;
        _selectedTable = null;
        this.RaisePropertyChanged(nameof(SelectedDatabase));
        this.RaisePropertyChanged(nameof(SelectedTable));
        SqlQuery = string.Empty;
        ErrorMessage = null;
        _workspaceId = null;
        _applicationName = null;
        this.RaisePropertyChanged(nameof(HasResults));
        this.RaisePropertyChanged(nameof(ShowEmptyState));
    }

    public async Task LoadAsync(string workspaceId, string applicationName, CancellationToken ct = default)
    {
        _activeLoad?.Cancel();
        var version = ++_loadVersion;
        using var load = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _activeLoad = load;
        _workspaceId = workspaceId;
        _applicationName = applicationName;
        Clear();
        _workspaceId = workspaceId;
        _applicationName = applicationName;
        IsLoading = true;
        try
        {
            var catalog = await _database.ListDatabasesAsync(workspaceId, applicationName, load.Token);
            if (version != _loadVersion
                || !string.Equals(workspaceId, _workspaceId, StringComparison.Ordinal)
                || !string.Equals(applicationName, _applicationName, StringComparison.Ordinal))
                return;

            Databases.Clear();
            foreach (var database in catalog.Databases)
                Databases.Add(database);

            SelectedDatabase = Databases.FirstOrDefault();
            ErrorMessage = Databases.Count == 0 ? "No databases found." : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (load.Token.IsCancellationRequested)
                return;

            ErrorMessage = ex is HttpRequestException ? "Could not load databases from the Server." : null;
            Trace.TraceWarning(ex.Message);
        }
        finally
        {
            if (version == _loadVersion)
                IsLoading = false;

            if (ReferenceEquals(_activeLoad, load))
                _activeLoad = null;
        }
    }

    private async Task LoadTablesAsync(string database)
    {
        if (_workspaceId is null || _applicationName is null)
            return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var tables = await _database.ListTablesAsync(_workspaceId, _applicationName, database);
            Tables.Clear();
            foreach (var table in tables.Tables)
                Tables.Add(table);

            SelectedTable = Tables.FirstOrDefault();
            if (Tables.Count == 0)
                ErrorMessage = "No tables found in this database.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = "Could not load tables from the Server.";
            Trace.TraceWarning(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RunQueryAsync()
    {
        if (_workspaceId is null || _applicationName is null || _selectedDatabase is null)
            return;

        if (string.IsNullOrWhiteSpace(SqlQuery))
            return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var result = await _database.ExecuteQueryAsync(
                _workspaceId,
                _applicationName,
                _selectedDatabase,
                SqlQuery);
            ApplyResult(result);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = "Query failed. Check the SQL and database connection.";
            Trace.TraceWarning(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyResult(DTOs.DatabaseQueryResultDto result)
    {
        Columns.Clear();
        Rows.Clear();
        foreach (var column in result.Columns)
            Columns.Add(column);

        foreach (var row in result.Rows)
            Rows.Add(new DatabaseRowViewModel(row));

        this.RaisePropertyChanged(nameof(HasResults));
        this.RaisePropertyChanged(nameof(ShowEmptyState));
    }

    private static string BuildDefaultQuery(string table)
    {
        var quoted = table.Contains('.')
            ? string.Join('.', table.Split('.').Select(QuoteIdentifier))
            : QuoteIdentifier(table);
        return $"SELECT * FROM {quoted} LIMIT 50";
    }

    private static string QuoteIdentifier(string identifier)
        => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
