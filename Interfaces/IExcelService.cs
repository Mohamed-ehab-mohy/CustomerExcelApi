using CustomerExcelApi.Entities;

namespace CustomerExcelApi.Interfaces;

public interface IExcelService
{
    IReadOnlyList<Customer> ReadCustomersFromExcel(
        Stream fileStream,
        CancellationToken cancellationToken = default);

    byte[] GenerateExcel(
        IReadOnlyList<string> columns,
        IReadOnlyList<Customer> rows);
}
