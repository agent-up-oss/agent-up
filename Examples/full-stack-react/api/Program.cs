using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;

var port = int.TryParse(Environment.GetEnvironmentVariable("API_PORT"), out var parsedPort) ? parsedPort : 5601;
var postgresHost = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "127.0.0.1";
var postgresPort = int.TryParse(Environment.GetEnvironmentVariable("POSTGRES_PORT"), out var parsedPgPort)
    ? parsedPgPort
    : 5432;
var connectionString = new NpgsqlConnectionStringBuilder
{
    Host = postgresHost,
    Port = postgresPort,
    Username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "agentup",
    Password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "agentup-example-local",
    Database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "agentup"
}.ConnectionString;

var metrics = new RuntimeMetrics();
var database = new DatabaseState(connectionString);
_ = database.WarmAsync();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
var app = builder.Build();

app.Use(async (context, next) =>
{
    var started = DateTimeOffset.UtcNow;
    await next();
    metrics.Record((DateTimeOffset.UtcNow - started).TotalMilliseconds, context.Response.StatusCode);
});

app.Use(async (context, next) =>
{
    context.Response.Headers.AccessControlAllowOrigin = "*";
    context.Response.Headers.AccessControlAllowHeaders = "Content-Type";
    await next();
});

app.MapGet("/health", async () =>
{
    try
    {
        await database.EnsureReadyAsync();
        await using var connection = await database.OpenAsync();
        await using var command = new NpgsqlCommand("select count(*)::int as product_count from products", connection);
        var count = (int)(await command.ExecuteScalarAsync() ?? 0);
        return Results.Json(new HealthResponse(true, "connected", count));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[health] {ex.Message}");
        return Results.Json(new HealthResponse(false, ex.Message, 0), statusCode: 503);
    }
});

app.MapGet("/metrics", async () =>
{
    try
    {
        await database.EnsureReadyAsync();
        await using var connection = await database.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            select
              (select count(*)::int from products) as product_count,
              (select count(*)::int from orders) as order_count
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        var snapshot = metrics.Snapshot();
        return Results.Json(new MetricsEnvelope(snapshot with
        {
            ProductCount = reader.GetInt32(0),
            OrderCount = reader.GetInt32(1)
        }));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[metrics] {ex.Message}");
        return Results.Json(new ErrorResponse(ex.Message), statusCode: 503);
    }
});

app.MapGet("/api/products", async () =>
{
    try
    {
        await database.EnsureReadyAsync();
        await using var connection = await database.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            select sku, name, category, status, region, inventory, unit_price, margin, updated_at
            from products
            order by name
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        var products = new List<ProductRow>();
        while (await reader.ReadAsync())
        {
            products.Add(new ProductRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5),
                reader.GetDecimal(6),
                reader.GetDecimal(7),
                reader.GetDateTime(8)));
        }

        var summary = products.Aggregate(
            new ProductSummary(0, 0, 0, 0),
            (current, product) => current with
            {
                ProductCount = current.ProductCount + 1,
                TotalInventory = current.TotalInventory + product.Inventory,
                InventoryValue = current.InventoryValue + product.Inventory * product.UnitPrice,
                LowStockCount = current.LowStockCount + (product.Inventory < 100 ? 1 : 0)
            });
        return Results.Json(new ProductsResponse(products, summary));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[products] {ex.Message}");
        return Results.Json(new ErrorResponse(ex.Message), statusCode: 503);
    }
});

app.MapGet("/api/orders", async () =>
{
    try
    {
        await database.EnsureReadyAsync();
        await using var connection = await database.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            select
              o.id,
              o.customer_name,
              o.region,
              o.status,
              o.total_amount,
              o.placed_at,
              count(oi.sku)::int as line_count
            from orders o
            left join order_items oi on oi.order_id = o.id
            group by o.id, o.customer_name, o.region, o.status, o.total_amount, o.placed_at
            order by o.placed_at desc
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        var orders = new List<OrderRow>();
        while (await reader.ReadAsync())
        {
            orders.Add(new OrderRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetDecimal(4),
                reader.GetDateTime(5),
                reader.GetInt32(6)));
        }

        return Results.Json(new OrdersResponse(orders));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[orders] {ex.Message}");
        return Results.Json(new ErrorResponse(ex.Message), statusCode: 503);
    }
});

Console.WriteLine($"API listening on {port}");
Console.WriteLine($"API querying Postgres at {postgresHost}:{postgresPort}/{Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "agentup"}");
app.Run();

internal sealed class DatabaseState(string connectionString)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _ready;
    private Task? _warmup;

    public Task WarmAsync() => _warmup ??= WarmLoopAsync();

    public async Task EnsureReadyAsync()
    {
        if (_ready)
            return;

        _ = WarmAsync();
        throw new InvalidOperationException("Database is not ready yet.");
    }

    public async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private async Task WarmLoopAsync()
    {
        var attempt = 0;
        while (!_ready)
        {
            attempt += 1;
            try
            {
                await using var connection = await OpenAsync();
                await MigrateAsync(connection);
                await SeedAsync(connection);
                _ready = true;
                Console.WriteLine($"[startup] database ready after {attempt} attempt(s)");
                return;
            }
            catch (Exception ex)
            {
                var delayMs = Math.Min(1000 * attempt, 5000);
                Console.WriteLine($"[startup] waiting for Postgres (attempt {attempt}): {ex.Message}");
                await Task.Delay(delayMs);
            }
        }
    }

    private static async Task MigrateAsync(NpgsqlConnection connection)
    {
        var migrationsDir = new[]
            {
                Path.Join(Directory.GetCurrentDirectory(), "..", "migrations"),
                Path.Join(AppContext.BaseDirectory, "migrations"),
                Path.Join(AppContext.BaseDirectory, "..", "..", "..", "..", "migrations")
            }
            .Select(Path.GetFullPath)
            .FirstOrDefault(Directory.Exists)
            ?? throw new DirectoryNotFoundException("Could not find the example SQL migrations directory.");

        foreach (var file in Directory.GetFiles(migrationsDir, "*.sql").Order(StringComparer.Ordinal))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            if (await IsAppliedAsync(connection, id))
                continue;

            var sql = await File.ReadAllTextAsync(file);
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                await using (var apply = new NpgsqlCommand(sql, connection, transaction))
                    await apply.ExecuteNonQueryAsync();
                await using (var record = new NpgsqlCommand(
                    "insert into schema_migrations (id) values ($1)", connection, transaction))
                {
                    record.Parameters.AddWithValue(id);
                    await record.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                Console.WriteLine($"[migrate] applied {Path.GetFileName(file)}");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }

    private static async Task<bool> IsAppliedAsync(NpgsqlConnection connection, string id)
    {
        try
        {
            await using var command = new NpgsqlCommand(
                "select 1 from schema_migrations where id = $1", connection);
            command.Parameters.AddWithValue(id);
            return await command.ExecuteScalarAsync() is not null;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return false;
        }
    }

    private async Task SeedAsync(NpgsqlConnection connection)
    {
        await _gate.WaitAsync();
        try
        {
            await using var countCommand = new NpgsqlCommand("select count(*)::int from products", connection);
            if ((int)(await countCommand.ExecuteScalarAsync() ?? 0) > 0)
                return;

            var seedDir = Path.Join(AppContext.BaseDirectory, "seed");
            if (!Directory.Exists(seedDir))
                seedDir = Path.Join(Directory.GetCurrentDirectory(), "seed");

            var products = JsonSerializer.Deserialize<List<ProductSeed>>(
                await File.ReadAllTextAsync(Path.Join(seedDir, "products.json")),
                SeedJson.Options) ?? [];
            var orders = JsonSerializer.Deserialize<List<OrderSeed>>(
                await File.ReadAllTextAsync(Path.Join(seedDir, "orders.json")),
                SeedJson.Options) ?? [];

            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                foreach (var product in products)
                {
                    await using var insert = new NpgsqlCommand(
                        """
                        insert into products (sku, name, category, status, region, inventory, unit_price, margin, updated_at)
                        values ($1, $2, $3, $4, $5, $6, $7, $8, $9)
                        """,
                        connection,
                        transaction);
                    insert.Parameters.AddWithValue(product.Sku);
                    insert.Parameters.AddWithValue(product.Name);
                    insert.Parameters.AddWithValue(product.Category);
                    insert.Parameters.AddWithValue(product.Status);
                    insert.Parameters.AddWithValue(product.Region);
                    insert.Parameters.AddWithValue(product.Inventory);
                    insert.Parameters.AddWithValue(product.UnitPrice);
                    insert.Parameters.AddWithValue(product.Margin);
                    insert.Parameters.AddWithValue(product.UpdatedAt);
                    await insert.ExecuteNonQueryAsync();
                }

                foreach (var order in orders)
                {
                    await using var insertOrder = new NpgsqlCommand(
                        """
                        insert into orders (id, customer_name, region, status, total_amount, placed_at)
                        values ($1, $2, $3, $4, $5, $6)
                        """,
                        connection,
                        transaction);
                    insertOrder.Parameters.AddWithValue(order.Id);
                    insertOrder.Parameters.AddWithValue(order.CustomerName);
                    insertOrder.Parameters.AddWithValue(order.Region);
                    insertOrder.Parameters.AddWithValue(order.Status);
                    insertOrder.Parameters.AddWithValue(order.TotalAmount);
                    insertOrder.Parameters.AddWithValue(order.PlacedAt);
                    await insertOrder.ExecuteNonQueryAsync();

                    foreach (var item in order.Items)
                    {
                        await using var insertItem = new NpgsqlCommand(
                            """
                            insert into order_items (order_id, sku, quantity, line_total)
                            values ($1, $2, $3, $4)
                            """,
                            connection,
                            transaction);
                        insertItem.Parameters.AddWithValue(order.Id);
                        insertItem.Parameters.AddWithValue(item.Sku);
                        insertItem.Parameters.AddWithValue(item.Quantity);
                        insertItem.Parameters.AddWithValue(item.LineTotal);
                        await insertItem.ExecuteNonQueryAsync();
                    }
                }

                await transaction.CommitAsync();
                Console.WriteLine($"[seed] inserted {products.Count} product(s) and {orders.Count} order(s)");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}

internal sealed class RuntimeMetrics
{
    private readonly ConcurrentQueue<(long At, double DurationMs)> _recent = [];
    private readonly long _startedAt = Environment.TickCount64;
    private int _total;
    private int _errors;

    public void Record(double durationMs, int statusCode)
    {
        Interlocked.Increment(ref _total);
        if (statusCode >= 400)
            Interlocked.Increment(ref _errors);
        _recent.Enqueue((Environment.TickCount64, durationMs));
        Prune();
    }

    public MetricsSnapshot Snapshot()
    {
        Prune();
        var recent = _recent.ToArray();
        var total = _total;
        var errors = _errors;
        var latency = recent.Length == 0 ? 0 : (int)Math.Round(recent.Average(entry => entry.DurationMs));
        var success = total == 0 ? 100 : Math.Round(((total - errors) / (double)total) * 1000) / 10;
        return new MetricsSnapshot(
            latency,
            recent.Length,
            errors,
            success,
            100,
            total,
            (int)Math.Round((Environment.TickCount64 - _startedAt) / 1000d),
            (int)Math.Round(GC.GetTotalMemory(false) / 1024d / 1024d),
            0,
            0);
    }

    private void Prune()
    {
        var cutoff = Environment.TickCount64 - 60_000;
        while (_recent.TryPeek(out var entry) && entry.At < cutoff)
            _recent.TryDequeue(out _);
    }
}

internal static class SeedJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
}

internal sealed record HealthResponse(
    bool Ok,
    string Database,
    [property: JsonPropertyName("product_count")] int ProductCount);

internal sealed record ErrorResponse(string Error);

internal sealed record MetricsEnvelope(MetricsSnapshot Metrics);

internal sealed record MetricsSnapshot(
    [property: JsonPropertyName("latency_ms")] int LatencyMs,
    [property: JsonPropertyName("requests_per_minute")] int RequestsPerMinute,
    [property: JsonPropertyName("errors_total")] int ErrorsTotal,
    [property: JsonPropertyName("success_rate")] double SuccessRate,
    [property: JsonPropertyName("uptime_percent")] int UptimePercent,
    [property: JsonPropertyName("requests_total")] int RequestsTotal,
    [property: JsonPropertyName("uptime_seconds")] int UptimeSeconds,
    [property: JsonPropertyName("heap_mb")] int HeapMb,
    [property: JsonPropertyName("product_count")] int ProductCount,
    [property: JsonPropertyName("order_count")] int OrderCount);

internal sealed record ProductRow(
    string Sku,
    string Name,
    string Category,
    string Status,
    string Region,
    int Inventory,
    [property: JsonPropertyName("unit_price")] decimal UnitPrice,
    decimal Margin,
    [property: JsonPropertyName("updated_at")] DateTime UpdatedAt);

internal sealed record ProductSummary(
    int ProductCount,
    int TotalInventory,
    decimal InventoryValue,
    int LowStockCount);

internal sealed record ProductsResponse(IReadOnlyList<ProductRow> Products, ProductSummary Summary);

internal sealed record OrderRow(
    string Id,
    [property: JsonPropertyName("customer_name")] string CustomerName,
    string Region,
    string Status,
    [property: JsonPropertyName("total_amount")] decimal TotalAmount,
    [property: JsonPropertyName("placed_at")] DateTime PlacedAt,
    [property: JsonPropertyName("line_count")] int LineCount);

internal sealed record OrdersResponse(IReadOnlyList<OrderRow> Orders);

internal sealed record ProductSeed(
    string Sku,
    string Name,
    string Category,
    string Status,
    string Region,
    int Inventory,
    decimal UnitPrice,
    decimal Margin,
    DateTime UpdatedAt);

internal sealed record OrderSeed(
    string Id,
    string CustomerName,
    string Region,
    string Status,
    decimal TotalAmount,
    DateTime PlacedAt,
    IReadOnlyList<OrderItemSeed> Items);

internal sealed record OrderItemSeed(string Sku, int Quantity, decimal LineTotal);
