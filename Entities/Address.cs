namespace CustomerExcelApi.Entities;

public sealed class Address
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;

    public Customer Customer { get; set; } = null!;
}
