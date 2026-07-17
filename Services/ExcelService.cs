using ClosedXML.Excel;
using CustomerExcelApi.Interfaces;

namespace CustomerExcelApi.Services;

public sealed class ExcelService : IExcelService
{
    private static readonly HashSet<string> ValidColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Name", "Email", "Street", "City", "Country",
        "ProductName", "Quantity", "Price", "OrderDate"
    };

    private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Name"] = "Name",
        ["Email"] = "Email",
        ["Street"] = "Street",
        ["City"] = "City",
        ["Country"] = "Country",
        ["ProductName"] = "Product Name",
        ["Quantity"] = "Quantity",
        ["Price"] = "Price",
        ["OrderDate"] = "Order Date"
    };

    public IReadOnlyList<CustomerImportRow> ReadCustomersFromExcel(
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheet(1);

        var lastRow = worksheet.LastRowUsed();
        var lastCol = worksheet.LastColumnUsed();

        if (lastRow is null || lastCol is null)
            return Array.Empty<CustomerImportRow>();

        var columnMap = BuildColumnMap(worksheet.Row(1), lastCol.ColumnNumber());
        if (columnMap.Count == 0)
            return Array.Empty<CustomerImportRow>();

        var rows = new List<CustomerImportRow>();
        var rowCount = lastRow.RowNumber();

        for (int row = 2; row <= rowCount; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var excelRow = worksheet.Row(row);
            if (IsRowEmpty(excelRow, lastCol.ColumnNumber()))
                continue;

            rows.Add(new CustomerImportRow
            {
                Name = ReadCell(excelRow, columnMap, "Name"),
                Email = ReadCell(excelRow, columnMap, "Email"),
                Street = ReadCell(excelRow, columnMap, "Street"),
                City = ReadCell(excelRow, columnMap, "City"),
                Country = ReadCell(excelRow, columnMap, "Country"),
                ProductName = ReadCell(excelRow, columnMap, "ProductName"),
                Quantity = int.TryParse(ReadCell(excelRow, columnMap, "Quantity"), out var q) ? q : 0,
                Price = decimal.TryParse(ReadCell(excelRow, columnMap, "Price"), out var p) ? p : 0,
                OrderDate = DateTime.TryParse(ReadCell(excelRow, columnMap, "OrderDate"), out var d) ? d : DateTime.MinValue
            });
        }

        return rows;
    }

    public byte[] GenerateExcel(
        IReadOnlyList<string> columns,
        IReadOnlyList<CustomerExportRow> rows)
    {
        var validColumns = columns
            .Where(c => ValidColumns.Contains(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Customers");

        for (int col = 0; col < validColumns.Count; col++)
        {
            var cell = worksheet.Cell(1, col + 1);
            cell.Value = DisplayNames.TryGetValue(validColumns[col], out var name)
                ? name : validColumns[col];
            cell.Style.Font.Bold = true;
        }

        for (int row = 0; row < rows.Count; row++)
        {
            var r = rows[row];
            for (int col = 0; col < validColumns.Count; col++)
            {
                worksheet.Cell(row + 2, col + 1).Value =
                    GetPropertyValue(r, validColumns[col]) ?? string.Empty;
            }
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static Dictionary<int, string> BuildColumnMap(IXLRow headerRow, int lastCol)
    {
        var map = new Dictionary<int, string>();

        for (int col = 1; col <= lastCol; col++)
        {
            var header = headerRow.Cell(col).GetString().Trim();
            if (string.IsNullOrEmpty(header)) continue;

            if (ValidColumns.Contains(header))
                map[col] = header;
            else
            {
                var match = ValidColumns.FirstOrDefault(
                    v => v.Equals(header, StringComparison.OrdinalIgnoreCase));
                if (match is not null) map[col] = match;
            }
        }

        return map;
    }

    private static bool IsRowEmpty(IXLRow row, int lastCol)
    {
        for (int col = 1; col <= lastCol; col++)
            if (!row.Cell(col).IsEmpty()) return false;
        return true;
    }

    private static string ReadCell(IXLRow row, Dictionary<int, string> columnMap, string property)
    {
        foreach (var kvp in columnMap)
            if (kvp.Value == property) return row.Cell(kvp.Key).GetString().Trim();
        return string.Empty;
    }

    private static string? GetPropertyValue(CustomerExportRow r, string property) =>
        property switch
        {
            "Name" => r.Name,
            "Email" => r.Email,
            "Street" => r.Street,
            "City" => r.City,
            "Country" => r.Country,
            "ProductName" => r.ProductName,
            "Quantity" => r.Quantity.ToString(),
            "Price" => r.Price.ToString("F2"),
            "OrderDate" => r.OrderDate.ToString("yyyy-MM-dd"),
            _ => null
        };
}
