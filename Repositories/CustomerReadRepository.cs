using System.Linq.Expressions;
using System.Reflection;
using CustomerExcelApi.Data;
using CustomerExcelApi.Entities;
using CustomerExcelApi.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CustomerExcelApi.Repositories;

public sealed class CustomerReadRepository : ICustomerReadRepository
{
    private readonly AppDbContext _db;

    private static readonly HashSet<string> ValidColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(Customer.Id),
        nameof(Customer.Name),
        nameof(Customer.Email),
        nameof(Customer.Address)
    };

    private static readonly PropertyInfo[] Properties =
        typeof(Customer).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    public CustomerReadRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Customer>> GetByColumnsAsync(
        IReadOnlyList<string> columns,
        CancellationToken cancellationToken = default)
    {
        var valid = columns
            .Where(c => ValidColumns.Contains(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (valid.Count == 0)
            return Array.Empty<Customer>();

        if (valid.Count == ValidColumns.Count)
            return await _db.Customers.AsNoTracking().ToListAsync(cancellationToken);

        var matchedProps = valid
            .Select(v => Properties.FirstOrDefault(
                p => p.Name.Equals(v, StringComparison.OrdinalIgnoreCase))!)
            .Where(p => p is not null)
            .ToList();

        var selector = BuildSelector(matchedProps);

        return await _db.Customers
            .AsNoTracking()
            .Select(selector)
            .ToListAsync(cancellationToken);
    }

    private static Expression<Func<Customer, Customer>> BuildSelector(List<PropertyInfo> properties)
    {
        var param = Expression.Parameter(typeof(Customer), "c");
        var bindings = new List<MemberBinding>();

        foreach (var prop in properties)
        {
            bindings.Add(Expression.Bind(prop, Expression.Property(param, prop)));
        }

        return Expression.Lambda<Func<Customer, Customer>>(
            Expression.MemberInit(Expression.New(typeof(Customer)), bindings), param);
    }
}
