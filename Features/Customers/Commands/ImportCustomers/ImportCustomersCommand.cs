namespace CustomerExcelApi.Features.Customers.Commands.ImportCustomers;

public sealed record ImportCustomersCommand
{
    public Stream FileStream { get; init; } = Stream.Null;
}
