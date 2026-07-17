using CustomerExcelApi.Entities;

namespace CustomerExcelApi.Interfaces;

public interface IExcelService
{
    IReadOnlyList<CustomerImportRow> ReadCustomersFromExcel(
        Stream fileStream,
        CancellationToken cancellationToken = default);

    byte[] GenerateExcel(
        IReadOnlyList<string> columns,
        IReadOnlyList<CustomerExportRow> rows);
}

public sealed class CustomerImportRow
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTime OrderDate { get; set; }
}

public sealed class CustomerExportRow
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTime OrderDate { get; set; }
}
