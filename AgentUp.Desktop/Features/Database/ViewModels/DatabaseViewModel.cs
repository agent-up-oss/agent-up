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
    private CancellationTokenSource? _activeTablesLoad;
    private CancellationTokenSource? _activeQuery;
    private long _loadVersion;
    private long _tablesVersion;
    private long _queryRequestId;
    private string? _workspaceId;
    private string? _applicationName;
    private string? _selectedDatabase;
    private string? _selectedTable;
    private string _sqlQuery = string.Empty;
    private bool _isLoading;
    private string? _errorMessage;

    public ObservableCollection<string> Databases { get; } = [];
    public ObservableCollection<string> Tables { get; } = [];
    public ObservableCollection<DatabaseColumnViewModel> Columns { get; } = [];
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
                RequestQuery();
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
        RunQueryCommand = ReactiveCommand.CreateFromTask(ExecuteRunQueryAsync);
    }

    public void Clear()
    {
        CancelPendingOperations();
        _loadVersion++;
        _tablesVersion++;
        _queryRequestId++;
        ResetVisibleState();
        _workspaceId = null;
        _applicationName = null;
    }

    public async Task LoadAsync(string workspaceId, string applicationName, CancellationToken ct = default)
    {
        CancelPendingOperations();
        var version = ++_loadVersion;
        _tablesVersion++;
        _queryRequestId++;
        using var load = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _activeLoad = load;
        _workspaceId = workspaceId;
        _applicationName = applicationName;
        ResetVisibleState();
        IsLoading = true;
        try
        {
            var catalog = await _database.ListDatabasesAsync(workspaceId, applicationName, load.Token);
            if (!IsCurrentLoad(version, workspaceId, applicationName))
                return;

            Databases.Clear();
            foreach (var database in catalog.Databases)
                Databases.Add(database);

            SelectedDatabase = Databases.FirstOrDefault();
            ErrorMessage = Databases.Count == 0 ? "No databases found." : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            if (load.Token.IsCancellationRequested || !IsCurrentLoad(version, workspaceId, applicationName))
                return;

            ErrorMessage = ex switch
            {
                HttpRequestException => "Could not load databases from the Server.",
                InvalidOperationException invalid => invalid.Message,
                _ => null
            };
            Trace.TraceWarning(ex.Message);
        }
        finally
        {
            if (IsCurrentLoad(version, workspaceId, applicationName))
                IsLoading = false;

            if (ReferenceEquals(_activeLoad, load))
                _activeLoad = null;
        }
    }

    private async Task LoadTablesAsync(string database)
    {
        if (_workspaceId is null || _applicationName is null)
            return;

        _activeTablesLoad?.Cancel();
        var version = ++_tablesVersion;
        using var load = new CancellationTokenSource();
        _activeTablesLoad = load;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var tables = await _database.ListTablesAsync(
                _workspaceId,
                _applicationName,
                database,
                load.Token);
            if (!IsCurrentTablesLoad(version, database))
                return;

            Tables.Clear();
            foreach (var table in tables.Tables)
                Tables.Add(table);

            SelectedTable = Tables.FirstOrDefault();
            if (Tables.Count == 0)
                ErrorMessage = "No tables found in this database.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            if (load.Token.IsCancellationRequested || !IsCurrentTablesLoad(version, database))
                return;

            ErrorMessage = ex switch
            {
                HttpRequestException => "Could not load tables from the Server.",
                InvalidOperationException invalid => invalid.Message,
                _ => null
            };
            Trace.TraceWarning(ex.Message);
        }
        finally
        {
            if (IsCurrentTablesLoad(version, database))
                IsLoading = false;

            if (ReferenceEquals(_activeTablesLoad, load))
                _activeTablesLoad = null;
        }
    }

    private long BeginQueryRequest()
        => Interlocked.Increment(ref _queryRequestId);

    private void RequestQuery()
    {
        var requestId = BeginQueryRequest();
        _ = RunQueryAsync(requestId);
    }

    public Task ExecuteRunQueryAsync()
    {
        var requestId = BeginQueryRequest();
        return RunQueryAsync(requestId);
    }

    private async Task RunQueryAsync(long requestId)
    {
        if (_workspaceId is null || _applicationName is null || _selectedDatabase is null)
            return;

        if (string.IsNullOrWhiteSpace(SqlQuery))
            return;

        if (requestId != _queryRequestId)
            return;

        _activeQuery?.Cancel();
        using var load = new CancellationTokenSource();
        _activeQuery = load;
        var database = _selectedDatabase;
        var sql = SqlQuery;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var result = await _database.ExecuteQueryAsync(
                _workspaceId,
                _applicationName,
                database,
                sql,
                load.Token);
            if (!IsCurrentQuery(requestId, database, sql))
                return;

            ApplyResult(result);
        }
        catch (InvalidOperationException ex)
        {
            if (load.Token.IsCancellationRequested || !IsCurrentQuery(requestId, database, sql))
                return;

            ErrorMessage = ex.Message;
            Columns.Clear();
            Rows.Clear();
            this.RaisePropertyChanged(nameof(HasResults));
            this.RaisePropertyChanged(nameof(ShowEmptyState));
            Trace.TraceWarning(ex.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (load.Token.IsCancellationRequested || !IsCurrentQuery(requestId, database, sql))
                return;

            ErrorMessage = "Query failed. Check the SQL and database connection.";
            Trace.TraceWarning(ex.Message);
        }
        finally
        {
            if (IsCurrentQuery(requestId, database, sql))
                IsLoading = false;

            if (ReferenceEquals(_activeQuery, load))
                _activeQuery = null;
        }
    }

    private void CancelPendingOperations()
    {
        _activeLoad?.Cancel();
        _activeTablesLoad?.Cancel();
        _activeQuery?.Cancel();
    }

    private void ResetVisibleState()
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
        this.RaisePropertyChanged(nameof(HasResults));
        this.RaisePropertyChanged(nameof(ShowEmptyState));
    }

    private bool IsCurrentLoad(long version, string workspaceId, string applicationName)
        => version == _loadVersion
           && string.Equals(workspaceId, _workspaceId, StringComparison.Ordinal)
           && string.Equals(applicationName, _applicationName, StringComparison.Ordinal);

    private bool IsCurrentTablesLoad(long version, string database)
        => version == _tablesVersion
           && string.Equals(database, _selectedDatabase, StringComparison.Ordinal)
           && _workspaceId is not null
           && _applicationName is not null;

    private bool IsCurrentQuery(long requestId, string database, string sql)
        => requestId == _queryRequestId
           && string.Equals(database, _selectedDatabase, StringComparison.Ordinal)
           && string.Equals(sql, SqlQuery, StringComparison.Ordinal)
           && _workspaceId is not null
           && _applicationName is not null;

    private void ApplyResult(DTOs.DatabaseQueryResultDto result)
    {
        Columns.Clear();
        Rows.Clear();

        var widths = BuildColumnWidths(result.Columns, result.Rows);
        for (var i = 0; i < result.Columns.Count; i++)
            Columns.Add(new DatabaseColumnViewModel(result.Columns[i], widths[i]));

        foreach (var row in result.Rows)
        {
            var cells = new List<DatabaseCellViewModel>(row.Count);
            for (var i = 0; i < row.Count; i++)
                cells.Add(new DatabaseCellViewModel(row[i], widths[Math.Min(i, widths.Count - 1)]));
            Rows.Add(new DatabaseRowViewModel(cells));
        }

        this.RaisePropertyChanged(nameof(HasResults));
        this.RaisePropertyChanged(nameof(ShowEmptyState));
    }

    private static IReadOnlyList<double> BuildColumnWidths(
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (columns.Count == 0)
            return [];

        var widths = new double[columns.Count];
        for (var i = 0; i < columns.Count; i++)
            widths[i] = EstimateColumnWidth(columns[i]);

        foreach (var row in rows)
        {
            for (var i = 0; i < columns.Count && i < row.Count; i++)
                widths[i] = Math.Max(widths[i], EstimateColumnWidth(row[i]));
        }

        return widths;
    }

    private static double EstimateColumnWidth(string value)
        => Math.Clamp(12 + (string.IsNullOrEmpty(value) ? 8 : value.Length) * 7.5, 120, 360);

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
