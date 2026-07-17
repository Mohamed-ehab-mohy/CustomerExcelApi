using CustomerExcelApi.Data;
using CustomerExcelApi.Entities;
using CustomerExcelApi.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace CustomerExcelApi.Repositories;

public sealed class CustomerBulkRepository : ICustomerBulkRepository
{
    private readonly AppDbContext _db;

    private const string CopyCustomers =
        """COPY "Customers" ("Id", "Name", "Email") FROM STDIN (FORMAT BINARY)""";
    private const string CopyAddresses =
        """COPY "Addresses" ("Id", "CustomerId", "Street", "City", "Country") FROM STDIN (FORMAT BINARY)""";
    private const string CopyOrders =
        """COPY "Orders" ("Id", "CustomerId", "ProductName", "Quantity", "Price", "OrderDate") FROM STDIN (FORMAT BINARY)""";

    public CustomerBulkRepository(AppDbContext db) => _db = db;

    public async Task<int> BulkInsertAsync(
        IReadOnlyList<CustomerImportRow> rows,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0) return 0;

        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        var wasClosed = connection.State == System.Data.ConnectionState.Closed;

        if (wasClosed)
            await connection.OpenAsync(cancellationToken);

        try
        {
            var existingCustomers = await LoadExistingCustomersAsync(connection, cancellationToken);
            var existingAddresses = await LoadExistingAddressesAsync(connection, cancellationToken);
            var existingOrders = await LoadExistingOrdersAsync(connection, cancellationToken);

            var customerMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

            await using (var importer = connection.BeginBinaryImport(CopyCustomers))
            {
                foreach (var row in rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var key = $"{row.Name}|{row.Email}";
                    if (customerMap.ContainsKey(key)) continue;
                    if (existingCustomers.Contains(key)) continue;

                    customerMap[key] = Guid.NewGuid();
                    await importer.StartRowAsync(cancellationToken);
                    await importer.WriteAsync(customerMap[key], NpgsqlDbType.Uuid, cancellationToken);
                    await importer.WriteAsync(row.Name, NpgsqlDbType.Varchar, cancellationToken);
                    await importer.WriteAsync(row.Email, NpgsqlDbType.Varchar, cancellationToken);
                }
                await importer.CompleteAsync(cancellationToken);
            }

            var addressSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await using (var importer = connection.BeginBinaryImport(CopyAddresses))
            {
                foreach (var row in rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(row.Street) &&
                        string.IsNullOrWhiteSpace(row.City) &&
                        string.IsNullOrWhiteSpace(row.Country))
                        continue;

                    var key = $"{row.Name}|{row.Email}";
                    if (!customerMap.TryGetValue(key, out var customerId))
                        continue;

                    var addrKey = $"{customerId}|{row.Street}|{row.City}|{row.Country}";
                    if (!addressSet.Add(addrKey)) continue;
                    if (existingAddresses.Contains(addrKey)) continue;

                    await importer.StartRowAsync(cancellationToken);
                    await importer.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid, cancellationToken);
                    await importer.WriteAsync(customerId, NpgsqlDbType.Uuid, cancellationToken);
                    await importer.WriteAsync(row.Street, NpgsqlDbType.Varchar, cancellationToken);
                    await importer.WriteAsync(row.City, NpgsqlDbType.Varchar, cancellationToken);
                    await importer.WriteAsync(row.Country, NpgsqlDbType.Varchar, cancellationToken);
                }
                await importer.CompleteAsync(cancellationToken);
            }

            int orderCount = 0;
            await using (var importer = connection.BeginBinaryImport(CopyOrders))
            {
                foreach (var row in rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(row.ProductName))
                        continue;

                    var key = $"{row.Name}|{row.Email}";
                    if (!customerMap.TryGetValue(key, out var customerId))
                        continue;

                    var orderKey = $"{customerId}|{row.ProductName}|{row.Quantity}|{row.Price}|{row.OrderDate:yyyy-MM-dd}";
                    if (existingOrders.Contains(orderKey)) continue;

                    await importer.StartRowAsync(cancellationToken);
                    await importer.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid, cancellationToken);
                    await importer.WriteAsync(customerId, NpgsqlDbType.Uuid, cancellationToken);
                    await importer.WriteAsync(row.ProductName, NpgsqlDbType.Varchar, cancellationToken);
                    await importer.WriteAsync(row.Quantity, NpgsqlDbType.Integer, cancellationToken);
                    await importer.WriteAsync(row.Price, NpgsqlDbType.Numeric, cancellationToken);
                    await importer.WriteAsync(row.OrderDate, NpgsqlDbType.Date, cancellationToken);
                    orderCount++;
                }
                await importer.CompleteAsync(cancellationToken);
            }

            return customerMap.Count + orderCount;
        }
        finally
        {
            if (wasClosed && connection.State == System.Data.ConnectionState.Open)
                await connection.CloseAsync();
        }
    }

    private static async Task<HashSet<string>> LoadExistingCustomersAsync(
        NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const string sql = """SELECT "Name", "Email" FROM "Customers" """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            set.Add($"{reader.GetString(0)}|{reader.GetString(1)}");
        }
        return set;
    }

    private static async Task<HashSet<string>> LoadExistingAddressesAsync(
        NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const string sql = """
            SELECT a."CustomerId", a."Street", a."City", a."Country"
            FROM "Addresses" a
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            set.Add($"{reader.GetGuid(0)}|{reader.GetString(1)}|{reader.GetString(2)}|{reader.GetString(3)}");
        }
        return set;
    }

    private static async Task<HashSet<string>> LoadExistingOrdersAsync(
        NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const string sql = """
            SELECT o."CustomerId", o."ProductName", o."Quantity", o."Price", o."OrderDate"
            FROM "Orders" o
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var date = reader.GetDateTime(4).ToString("yyyy-MM-dd");
            set.Add($"{reader.GetGuid(0)}|{reader.GetString(1)}|{reader.GetInt32(2)}|{reader.GetDecimal(3)}|{date}");
        }
        return set;
    }
}
