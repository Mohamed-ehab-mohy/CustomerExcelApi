using CustomerExcelApi.Entities;

namespace CustomerExcelApi.Interfaces;

public interface ICustomerBulkRepository
{
    Task<int> BulkInsertAsync(
        IReadOnlyList<Customer> customers,
        CancellationToken cancellationToken = default);
}
