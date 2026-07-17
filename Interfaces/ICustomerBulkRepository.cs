using CustomerExcelApi.Entities;

namespace CustomerExcelApi.Interfaces;

public interface ICustomerBulkRepository
{
    Task<int> BulkInsertAsync(
        IReadOnlyList<CustomerImportRow> rows,
        CancellationToken cancellationToken = default);
}
