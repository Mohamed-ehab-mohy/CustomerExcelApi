# CustomerExcelApi

ASP.NET Core 8 Web API for importing and exporting customer data via Excel files. Built with PostgreSQL, ClosedXML, and deployed on Railway.

**Live API:** https://customerexcelapi-production.up.railway.app/swagger/index.html

---

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Framework | ASP.NET Core 8.0 |
| Database | PostgreSQL 15 |
| ORM | Entity Framework Core 8.0 |
| Excel Library | ClosedXML 0.102.3 |
| Bulk Insert | Npgsql Binary COPY |
| API Docs | Swagger / Swashbuckle |
| Container | Docker (multi-stage build) |
| Hosting | Railway |

---

## Project Structure

```
CustomerExcelApi/
├── Controllers/
│   └── CustomersController.cs          # API endpoints
├── Data/
│   ├── AppDbContext.cs                  # EF Core DbContext
│   └── Configurations/
│       └── CustomerConfiguration.cs     # Entity config
├── Entities/
│   └── Customer.cs                      # Domain model
├── Features/
│   └── Customers/
│       ├── Commands/ImportCustomers/    # Import command + handler
│       ├── DTOs/                        # Data transfer objects
│       └── Queries/ExportCustomers/     # Export query + handler
├── Interfaces/                          # Repository + service contracts
├── Migrations/                          # EF Core migrations
├── Repositories/
│   ├── CustomerBulkRepository.cs        # Bulk insert via Npgsql COPY
│   └── CustomerReadRepository.cs        # Dynamic column projection
├── Services/
│   └── ExcelService.cs                  # Excel read/write logic
├── Dockerfile
└── Program.cs                           # App entry point
```

---

## Customer Model

```json
{
  "id": "guid (auto-generated)",
  "name": "string (max 200 chars)",
  "email": "string (max 200 chars)",
  "address": "string (max 500 chars)"
}
```

| Field | Type | Required | Max Length |
|-------|------|----------|------------|
| `Id` | UUID (Guid) | Yes | - |
| `Name` | string | Yes | 200 |
| `Email` | string | Yes | 200 |
| `Address` | string | Yes | 500 |

---

## API Endpoints

Base URL: `https://customerexcelapi-production.up.railway.app`

---

### POST `/api/customers/import`

Upload an Excel file (.xlsx) to import customers into the database.

#### Request

- **Content-Type:** `multipart/form-data`
- **Max File Size:** 50 MB

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | File (.xlsx) | Yes | Excel file with customer data |

#### Excel File Format

The Excel file must have a **header row** (row 1) with column names matching the model fields. Column names are **case-insensitive**.

| Column Header | Maps To | Required |
|---------------|---------|----------|
| `Name` | Customer.Name | Yes |
| `Email` | Customer.Email | Yes |
| `Address` | Customer.Address | Yes |
| `Id` | Customer.Id | Optional (auto-generated if omitted) |

**Example Excel layout:**

| Name | Email | Address |
|------|-------|---------|
| Ahmed | ahmed@test.com | Cairo |
| Sara | sara@test.com | Alex |

#### Response

**Status:** `200 OK`

```json
{
  "totalRows": 3,
  "inserted": 3,
  "durationMs": 220
}
```

| Field | Type | Description |
|-------|------|-------------|
| `totalRows` | int | Total rows parsed from the Excel file |
| `inserted` | int | Number of rows inserted into the database |
| `durationMs` | long | Time taken for the operation in milliseconds |

**Error Responses:**

| Status | Body | Cause |
|--------|------|-------|
| `400` | `{"error": "No file uploaded."}` | No file or empty file sent |
| `500` | `{"error": "..."}` | Server error (corrupt file, DB error, etc.) |

#### cURL Example

```bash
curl -X POST https://customerexcelapi-production.up.railway.app/api/customers/import \
  -F "file=@customers.xlsx"
```

#### JavaScript Example

```javascript
async function importCustomers(file) {
  const formData = new FormData();
  formData.append('file', file);

  const response = await fetch(
    'https://customerexcelapi-production.up.railway.app/api/customers/import',
    {
      method: 'POST',
      body: formData,
    }
  );

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.error);
  }

  const result = await response.json();
  console.log(`Imported ${result.inserted} of ${result.totalRows} rows in ${result.durationMs}ms`);
  return result;
}

// Usage with file input
document.getElementById('fileInput').addEventListener('change', async (e) => {
  const file = e.target.files[0];
  if (file) {
    const result = await importCustomers(file);
    alert(`Successfully imported ${result.inserted} customers!`);
  }
});
```

---

### POST `/api/customers/export`

Export customers from the database as an Excel file (.xlsx). Supports dynamic column selection.

#### Request

- **Content-Type:** `application/json`

```json
{
  "columns": ["Name", "Email", "Address"]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `columns` | string[] | Yes | List of column names to include in the export |

#### Available Columns

| Column Name | Description |
|-------------|-------------|
| `Id` | Customer UUID |
| `Name` | Customer name |
| `Email` | Customer email |
| `Address` | Customer address |

- Column names are **case-insensitive**
- Invalid/unknown columns are **silently ignored**
- If empty array or no valid columns provided, returns empty Excel file

#### Response

**Status:** `200 OK`

- **Content-Type:** `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- **Content-Disposition:** `attachment; filename=customers.xlsx`
- **Body:** Binary Excel file (.xlsx)

#### cURL Example

```bash
curl -X POST https://customerexcelapi-production.up.railway.app/api/customers/export \
  -H "Content-Type: application/json" \
  -d '{"columns": ["Name", "Email", "Address"]}' \
  --output customers.xlsx
```

#### JavaScript Example

```javascript
async function exportCustomers(columns = ['Name', 'Email', 'Address']) {
  const response = await fetch(
    'https://customerexcelapi-production.up.railway.app/api/customers/export',
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ columns }),
    }
  );

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.error);
  }

  // Download the file
  const blob = await response.blob();
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = 'customers.xlsx';
  document.body.appendChild(a);
  a.click();
  a.remove();
  window.URL.revokeObjectURL(url);
}

// Usage
exportCustomers(['Name', 'Email']);       // Export only Name and Email
exportCustomers(['Name', 'Email', 'Address']); // Export all fields
exportCustomers(['Id', 'Name']);           // Export Id and Name
```

---

## Frontend Integration Guide

### Complete HTML Example

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Customer Excel Manager</title>
    <style>
        body { font-family: Arial, sans-serif; max-width: 800px; margin: 40px auto; padding: 0 20px; }
        .section { border: 1px solid #ddd; padding: 20px; margin: 20px 0; border-radius: 8px; }
        button { padding: 10px 20px; margin: 5px; cursor: pointer; border: none; border-radius: 4px; background: #007bff; color: white; }
        button:hover { background: #0056b3; }
        .result { margin-top: 10px; padding: 10px; background: #f8f9fa; border-radius: 4px; }
        .error { background: #f8d7da; color: #721c24; }
        .success { background: #d4edda; color: #155724; }
    </style>
</head>
<body>
    <h1>Customer Excel Manager</h1>

    <!-- Import Section -->
    <div class="section">
        <h2>Import Customers</h2>
        <p>Upload an Excel file (.xlsx) with columns: Name, Email, Address</p>
        <input type="file" id="fileInput" accept=".xlsx" />
        <button onclick="handleImport()">Import</button>
        <div id="importResult"></div>
    </div>

    <!-- Export Section -->
    <div class="section">
        <h2>Export Customers</h2>
        <label><input type="checkbox" value="Id" class="col-check"> Id</label>
        <label><input type="checkbox" value="Name" class="col-check" checked> Name</label>
        <label><input type="checkbox" value="Email" class="col-check" checked> Email</label>
        <label><input type="checkbox" value="Address" class="col-check" checked> Address</label>
        <br><br>
        <button onclick="handleExport()">Export to Excel</button>
        <div id="exportResult"></div>
    </div>

    <script>
        const API_BASE = 'https://customerexcelapi-production.up.railway.app/api/customers';

        async function handleImport() {
            const fileInput = document.getElementById('fileInput');
            const resultDiv = document.getElementById('importResult');

            if (!fileInput.files.length) {
                resultDiv.className = 'result error';
                resultDiv.textContent = 'Please select a file first.';
                return;
            }

            const formData = new FormData();
            formData.append('file', fileInput.files[0]);

            try {
                resultDiv.className = 'result';
                resultDiv.textContent = 'Importing...';

                const response = await fetch(`${API_BASE}/import`, {
                    method: 'POST',
                    body: formData,
                });

                const data = await response.json();

                if (!response.ok) {
                    throw new Error(data.error || 'Import failed');
                }

                resultDiv.className = 'result success';
                resultDiv.textContent = `Successfully imported ${data.inserted} of ${data.totalRows} rows in ${data.durationMs}ms`;
            } catch (err) {
                resultDiv.className = 'result error';
                resultDiv.textContent = `Error: ${err.message}`;
            }
        }

        async function handleExport() {
            const checkboxes = document.querySelectorAll('.col-check:checked');
            const columns = Array.from(checkboxes).map(cb => cb.value);
            const resultDiv = document.getElementById('exportResult');

            if (columns.length === 0) {
                resultDiv.className = 'result error';
                resultDiv.textContent = 'Please select at least one column.';
                return;
            }

            try {
                resultDiv.className = 'result';
                resultDiv.textContent = 'Exporting...';

                const response = await fetch(`${API_BASE}/export`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ columns }),
                });

                if (!response.ok) {
                    const err = await response.json();
                    throw new Error(err.error || 'Export failed');
                }

                const blob = await response.blob();
                const url = window.URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = 'customers.xlsx';
                document.body.appendChild(a);
                a.click();
                a.remove();
                window.URL.revokeObjectURL(url);

                resultDiv.className = 'result success';
                resultDiv.textContent = 'Download started successfully!';
            } catch (err) {
                resultDiv.className = 'result error';
                resultDiv.textContent = `Error: ${err.message}`;
            }
        }
    </script>
</body>
</html>
```

### React Example

```jsx
const API_BASE = 'https://customerexcelapi-production.up.railway.app/api/customers';

function CustomerManager() {
  const [importing, setImporting] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [message, setMessage] = useState(null);

  const handleImport = async (e) => {
    const file = e.target.files[0];
    if (!file) return;

    setImporting(true);
    const formData = new FormData();
    formData.append('file', file);

    try {
      const res = await fetch(`${API_BASE}/import`, { method: 'POST', body: formData });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error);
      setMessage({ type: 'success', text: `Imported ${data.inserted}/${data.totalRows} rows in ${data.durationMs}ms` });
    } catch (err) {
      setMessage({ type: 'error', text: err.message });
    } finally {
      setImporting(false);
    }
  };

  const handleExport = async (columns) => {
    setExporting(true);
    try {
      const res = await fetch(`${API_BASE}/export`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ columns }),
      });
      if (!res.ok) throw new Error((await res.json()).error);
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'customers.xlsx';
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      setMessage({ type: 'error', text: err.message });
    } finally {
      setExporting(false);
    }
  };

  return (
    <div>
      <input type="file" accept=".xlsx" onChange={handleImport} disabled={importing} />
      <button onClick={() => handleExport(['Name', 'Email', 'Address'])} disabled={exporting}>
        Export All
      </button>
      {message && <p className={message.type}>{message.text}</p>}
    </div>
  );
}
```

---

## Error Handling

All error responses follow this format:

```json
{
  "error": "Primary error message",
  "inner": "Inner exception message (if any)",
  "stack": "Stack trace (development only)"
}
```

| Status | Meaning |
|--------|---------|
| `200` | Success |
| `400` | Bad request (no file, invalid input) |
| `500` | Server error (DB connection, corrupt file, etc.) |

---

## Performance Notes

- **Import** uses Npgsql binary `COPY` protocol for high-performance bulk inserts (not individual `INSERT` statements)
- **Export** uses dynamic LINQ expression trees for efficient column projection (no unnecessary data transfer)
- File size limit is **50 MB**
- Excel parsing uses **ClosedXML** (OpenXML-based, no Excel installation required)

---

## Environment Variables (Railway)

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `ASPNETCORE_ENVIRONMENT` | `Production` or `Development` |
| `WEBSITES_PORT` | `8080` (if using Azure App Service) |

---

## Local Development

```bash
# Clone
git clone https://github.com/Mohamed-ehab-mohy/CustomerExcelApi.git
cd CustomerExcelApi

# Run
dotnet run

# Open Swagger
# http://localhost:5247/swagger
```

Requires `appsettings.Development.json` with your local database connection string (not committed to git).

---

## License

MIT
