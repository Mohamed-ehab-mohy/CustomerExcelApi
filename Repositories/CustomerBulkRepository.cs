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
            var customerMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

            await using (var importer = connection.BeginBinaryImport(CopyCustomers))
            {
                foreach (var row in rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var key = $"{row.Name}|{row.Email}";
                    if (!customerMap.ContainsKey(key))
                    {
                        customerMap[key] = Guid.NewGuid();
                        await importer.StartRowAsync(cancellationToken);
                        await importer.WriteAsync(customerMap[key], NpgsqlDbType.Uuid, cancellationToken);
                        await importer.WriteAsync(row.Name, NpgsqlDbType.Varchar, cancellationToken);
                        await importer.WriteAsync(row.Email, NpgsqlDbType.Varchar, cancellationToken);
                    }
                }
                await importer.CompleteAsync(cancellationToken);
            }

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
}
