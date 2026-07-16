namespace CustomerExcelApi.Interfaces;

public interface ICustomerReadRepository
{
    Task<IReadOnlyList<Entities.Customer>> GetByColumnsAsync(
        IReadOnlyList<string> columns,
        CancellationToken cancellationToken = default);
}
