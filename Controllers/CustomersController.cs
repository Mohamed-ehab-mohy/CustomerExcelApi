using CustomerExcelApi.Features.Customers.Commands.ImportCustomers;
using CustomerExcelApi.Features.Customers.DTOs;
using CustomerExcelApi.Features.Customers.Queries.ExportCustomers;
using Microsoft.AspNetCore.Mvc;

namespace CustomerExcelApi.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly ImportCustomersHandler _importHandler;
    private readonly ExportCustomersHandler _exportHandler;

    public CustomersController(
        ImportCustomersHandler importHandler,
        ExportCustomersHandler exportHandler)
    {
        _importHandler = importHandler;
        _exportHandler = exportHandler;
    }

    [HttpPost("import")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> Import(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        var command = new ImportCustomersCommand
        {
            FileStream = file.OpenReadStream()
        };

        var result = await _importHandler.HandleAsync(command, cancellationToken);

        await command.FileStream.DisposeAsync();

        return Ok(result);
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export(
        [FromBody] ExportRequestDto request,
        CancellationToken cancellationToken)
    {
        var query = new ExportCustomersQuery { Request = request };

        var fileBytes = await _exportHandler.HandleAsync(query, cancellationToken);

        return File(
            fileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "customers.xlsx");
    }
}
