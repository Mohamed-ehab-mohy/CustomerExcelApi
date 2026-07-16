using CustomerExcelApi.Features.Customers.DTOs;

namespace CustomerExcelApi.Features.Customers.Queries.ExportCustomers;

public sealed record ExportCustomersQuery
{
    public ExportRequestDto Request { get; init; } = new();
}
