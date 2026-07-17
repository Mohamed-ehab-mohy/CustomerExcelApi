namespace CustomerExcelApi.Features.Customers.DTOs;

public sealed record ExportRequestDto
{
    public List<string> Columns { get; init; } = new();
}

public sealed record ImportResultDto
{
    public int TotalRows { get; init; }
    public int Inserted { get; init; }
    public long DurationMs { get; init; }
}
