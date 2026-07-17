using CustomerExcelApi.Data;
using CustomerExcelApi.Entities;
using CustomerExcelApi.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CustomerExcelApi.Repositories;

public sealed class CustomerReadRepository : ICustomerReadRepository
{
    private readonly AppDbContext _db;

    public CustomerReadRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<CustomerExportRow>> GetByColumnsAsync(
        IReadOnlyList<string> columns,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Customers
            .AsNoTracking()
            .SelectMany(c => c.Orders.DefaultIfEmpty(),
                (c, o) => new { c, o })
            .SelectMany(x => x.c.Addresses.DefaultIfEmpty(),
                (x, a) => new CustomerExportRow
                {
                    Name = x.c.Name,
                    Email = x.c.Email,
                    Street = a != null ? a.Street : string.Empty,
                    City = a != null ? a.City : string.Empty,
                    Country = a != null ? a.Country : string.Empty,
                    ProductName = x.o != null ? x.o.ProductName : string.Empty,
                    Quantity = x.o != null ? x.o.Quantity : 0,
                    Price = x.o != null ? x.o.Price : 0,
                    OrderDate = x.o != null ? x.o.OrderDate : DateTime.MinValue
                });

        return await query.ToListAsync(cancellationToken);
    }
}
