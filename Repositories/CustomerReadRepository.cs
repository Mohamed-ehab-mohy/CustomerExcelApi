using CustomerExcelApi.Data;
using CustomerExcelApi.Entities;
using CustomerExcelApi.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CustomerExcelApi.Repositories;

public sealed class CustomerReadRepository : ICustomerReadRepository
{
    private readonly AppDbContext _db;

    private static readonly HashSet<string> AddressColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Street", "City", "Country"
    };

    private static readonly HashSet<string> OrderColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "ProductName", "Quantity", "Price", "OrderDate",
        "Product Name", "Order Date"
    };

    public CustomerReadRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<CustomerExportRow>> GetByColumnsAsync(
        IReadOnlyList<string> columns,
        CancellationToken cancellationToken = default)
    {
        var validColumns = columns
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (validColumns.Count == 0)
        {
            return await _db.Customers.AsNoTracking()
                .Select(c => new CustomerExportRow
                {
                    Name = c.Name,
                    Email = c.Email
                })
                .ToListAsync(cancellationToken);
        }

        bool needsAddress = validColumns.Any(c => AddressColumns.Contains(c));
        bool needsOrders = validColumns.Any(c => OrderColumns.Contains(c));

        if (!needsAddress && !needsOrders)
        {
            return await _db.Customers.AsNoTracking()
                .Select(c => new CustomerExportRow
                {
                    Name = c.Name,
                    Email = c.Email
                })
                .ToListAsync(cancellationToken);
        }

        if (needsAddress && !needsOrders)
        {
            return await _db.Customers.AsNoTracking()
                .SelectMany(c => c.Addresses.DefaultIfEmpty(),
                    (c, a) => new CustomerExportRow
                    {
                        Name = c.Name,
                        Email = c.Email,
                        Street = a != null ? a.Street : string.Empty,
                        City = a != null ? a.City : string.Empty,
                        Country = a != null ? a.Country : string.Empty
                    })
                .ToListAsync(cancellationToken);
        }

        if (!needsAddress && needsOrders)
        {
            return await _db.Customers.AsNoTracking()
                .SelectMany(c => c.Orders.DefaultIfEmpty(),
                    (c, o) => new CustomerExportRow
                    {
                        Name = c.Name,
                        Email = c.Email,
                        ProductName = o != null ? o.ProductName : string.Empty,
                        Quantity = o != null ? o.Quantity : 0,
                        Price = o != null ? o.Price : 0,
                        OrderDate = o != null ? o.OrderDate : DateTime.MinValue
                    })
                .ToListAsync(cancellationToken);
        }

        return await _db.Customers.AsNoTracking()
            .SelectMany(c => c.Addresses.DefaultIfEmpty(),
                (c, a) => new { c, a })
            .SelectMany(x => x.c.Orders.DefaultIfEmpty(),
                (x, o) => new CustomerExportRow
                {
                    Name = x.c.Name,
                    Email = x.c.Email,
                    Street = x.a != null ? x.a.Street : string.Empty,
                    City = x.a != null ? x.a.City : string.Empty,
                    Country = x.a != null ? x.a.Country : string.Empty,
                    ProductName = o != null ? o.ProductName : string.Empty,
                    Quantity = o != null ? o.Quantity : 0,
                    Price = o != null ? o.Price : 0,
                    OrderDate = o != null ? o.OrderDate : DateTime.MinValue
                })
            .ToListAsync(cancellationToken);
    }
}
