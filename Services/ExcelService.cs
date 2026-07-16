using ClosedXML.Excel;
using CustomerExcelApi.Entities;
using CustomerExcelApi.Interfaces;

namespace CustomerExcelApi.Services;

public sealed class ExcelService : IExcelService
{
    private static readonly HashSet<string> ValidColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(Customer.Id),
        nameof(Customer.Name),
        nameof(Customer.Email),
        nameof(Customer.Address)
    };

    private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(Customer.Id)] = "Id",
        [nameof(Customer.Name)] = "Name",
        [nameof(Customer.Email)] = "Email",
        [nameof(Customer.Address)] = "Address"
    };

    public IReadOnlyList<Customer> ReadCustomersFromExcel(
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheet(1);

        var lastRow = worksheet.LastRowUsed();
        var lastCol = worksheet.LastColumnUsed();

        if (lastRow is null || lastCol is null)
            return Array.Empty<Customer>();

        var columnMap = BuildColumnMap(worksheet.Row(1), lastCol.ColumnNumber());
        if (columnMap.Count == 0)
            return Array.Empty<Customer>();

        var customers = new List<Customer>();
        var rowCount = lastRow.RowNumber();

        for (int row = 2; row <= rowCount; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var excelRow = worksheet.Row(row);
            if (IsRowEmpty(excelRow, lastCol.ColumnNumber()))
                continue;

            customers.Add(new Customer
            {
                Id = Guid.NewGuid(),
                Name = ReadCell(excelRow, columnMap, nameof(Customer.Name)),
                Email = ReadCell(excelRow, columnMap, nameof(Customer.Email)),
                Address = ReadCell(excelRow, columnMap, nameof(Customer.Address))
            });
        }

        return customers;
    }

    public byte[] GenerateExcel(
        IReadOnlyList<string> columns,
        IReadOnlyList<Customer> rows)
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
            var c = rows[row];
            for (int col = 0; col < validColumns.Count; col++)
            {
                worksheet.Cell(row + 2, col + 1).Value =
                    GetPropertyValue(c, validColumns[col]) ?? string.Empty;
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

    private static string? GetPropertyValue(Customer c, string property) =>
        string.Equals(property, nameof(Customer.Id), StringComparison.OrdinalIgnoreCase) ? c.Id.ToString() :
        string.Equals(property, nameof(Customer.Name), StringComparison.OrdinalIgnoreCase) ? c.Name :
        string.Equals(property, nameof(Customer.Email), StringComparison.OrdinalIgnoreCase) ? c.Email :
        string.Equals(property, nameof(Customer.Address), StringComparison.OrdinalIgnoreCase) ? c.Address :
        null;
}
