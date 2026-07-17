using CustomerExcelApi.Entities;

namespace CustomerExcelApi.Interfaces;

public interface ICustomerReadRepository
{
    Task<IReadOnlyList<CustomerExportRow>> GetByColumnsAsync(
        IReadOnlyList<string> columns,
        CancellationToken cancellationToken = default);
}
