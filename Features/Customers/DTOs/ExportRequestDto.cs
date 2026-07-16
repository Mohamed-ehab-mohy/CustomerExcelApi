namespace CustomerExcelApi.Features.Customers.DTOs;

public sealed record ExportRequestDto
{
    public List<string> Columns { get; init; } = new();
}
