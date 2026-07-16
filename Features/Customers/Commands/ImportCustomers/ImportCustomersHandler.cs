using System.Diagnostics;
using CustomerExcelApi.Features.Customers.DTOs;
using CustomerExcelApi.Interfaces;

namespace CustomerExcelApi.Features.Customers.Commands.ImportCustomers;

public sealed class ImportCustomersHandler
{
    private readonly IExcelService _excelService;
    private readonly ICustomerBulkRepository _bulkRepository;

    public ImportCustomersHandler(
        IExcelService excelService,
        ICustomerBulkRepository bulkRepository)
    {
        _excelService = excelService;
        _bulkRepository = bulkRepository;
    }

    public async Task<ImportResultDto> HandleAsync(
        ImportCustomersCommand command,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var customers = _excelService.ReadCustomersFromExcel(
            command.FileStream,
            cancellationToken);

        var inserted = await _bulkRepository.BulkInsertAsync(
            customers,
            cancellationToken);

        stopwatch.Stop();

        return new ImportResultDto
        {
            TotalRows = customers.Count,
            Inserted = inserted,
            DurationMs = stopwatch.ElapsedMilliseconds
        };
    }
}
