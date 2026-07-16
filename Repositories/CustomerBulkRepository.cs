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

    private const string CopyCommand =
        """
        COPY "Customers" ("Id", "Name", "Email", "Address")
        FROM STDIN (FORMAT BINARY)
        """;

    public CustomerBulkRepository(AppDbContext db) => _db = db;

    public async Task<int> BulkInsertAsync(
        IReadOnlyList<Customer> customers,
        CancellationToken cancellationToken = default)
    {
        if (customers.Count == 0) return 0;

        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        var wasClosed = connection.State == System.Data.ConnectionState.Closed;

        if (wasClosed)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var importer = connection.BeginBinaryImport(CopyCommand);

            foreach (var c in customers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await importer.StartRowAsync(cancellationToken);
                await importer.WriteAsync(c.Id, NpgsqlDbType.Uuid, cancellationToken);
                await importer.WriteAsync(c.Name, NpgsqlDbType.Varchar, cancellationToken);
                await importer.WriteAsync(c.Email, NpgsqlDbType.Varchar, cancellationToken);
                await importer.WriteAsync(c.Address, NpgsqlDbType.Varchar, cancellationToken);
            }

            var rowCount = await importer.CompleteAsync(cancellationToken);
            return (int)rowCount;
        }
        finally
        {
            if (wasClosed && connection.State == System.Data.ConnectionState.Open)
                await connection.CloseAsync();
        }
    }
}
