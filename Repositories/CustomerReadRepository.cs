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
        var query = from c in _db.Customers.AsNoTracking()
                    from a in c.Addresses.DefaultIfEmpty()
                    from o in c.Orders.DefaultIfEmpty()
                    select new CustomerExportRow
                    {
                        Name = c.Name,
                        Email = c.Email,
                        Street = a != null ? a.Street : string.Empty,
                        City = a != null ? a.City : string.Empty,
                        Country = a != null ? a.Country : string.Empty,
                        ProductName = o != null ? o.ProductName : string.Empty,
                        Quantity = o != null ? o.Quantity : 0,
                        Price = o != null ? o.Price : 0,
                        OrderDate = o != null ? o.OrderDate : DateTime.MinValue
                    };

        return await query.ToListAsync(cancellationToken);
    }
}
