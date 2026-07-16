namespace CustomerExcelApi.Features.Customers.DTOs;

public sealed record ImportResultDto
{
    public int TotalRows { get; init; }
    public int Inserted { get; init; }
    public long DurationMs { get; init; }
}
