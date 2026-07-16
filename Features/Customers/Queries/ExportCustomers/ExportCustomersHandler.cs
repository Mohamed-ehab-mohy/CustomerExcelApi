using CustomerExcelApi.Features.Customers.DTOs;
using CustomerExcelApi.Interfaces;

namespace CustomerExcelApi.Features.Customers.Queries.ExportCustomers;

public sealed class ExportCustomersHandler
{
    private readonly ICustomerReadRepository _readRepository;
    private readonly IExcelService _excelService;

    public ExportCustomersHandler(
        ICustomerReadRepository readRepository,
        IExcelService excelService)
    {
        _readRepository = readRepository;
        _excelService = excelService;
    }

    public async Task<byte[]> HandleAsync(
        ExportCustomersQuery query,
        CancellationToken cancellationToken = default)
    {
        var customers = await _readRepository.GetByColumnsAsync(
            query.Request.Columns,
            cancellationToken);

        return _excelService.GenerateExcel(query.Request.Columns, customers);
    }
}
