# CustomerExcelApi

ASP.NET Core 8 Web API for importing and exporting customer data via Excel files. Supports multi-table schema (Customers + Addresses + Orders) with dynamic column selection.

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

## Database Schema

### ER Diagram

```
┌──────────────┐       ┌──────────────────┐
│  Customers   │       │    Addresses     │
├──────────────┤       ├──────────────────┤
│ Id (PK)      │──┐    │ Id (PK)          │
│ Name         │  └───>│ CustomerId (FK)  │
│ Email        │       │ Street           │
└──────────────┘       │ City             │
       │               │ Country          │
       │               └──────────────────┘
       │
       │               ┌──────────────────┐
       │               │     Orders       │
       │               ├──────────────────┤
       └──────────────>│ Id (PK)          │
                       │ CustomerId (FK)  │
                       │ ProductName      │
                       │ Quantity         │
                       │ Price            │
                       │ OrderDate        │
                       └──────────────────┘
```

### Customers Table

| Column | Type | Required | Max Length |
|--------|------|----------|------------|
| `Id` | UUID | Yes (auto) | - |
| `Name` | string | Yes | 200 |
| `Email` | string | Yes | 200 |

### Addresses Table

| Column | Type | Required | Max Length |
|--------|------|----------|------------|
| `Id` | UUID | Yes (auto) | - |
| `CustomerId` | UUID (FK) | Yes | - |
| `Street` | string | Yes | 300 |
| `City` | string | Yes | 100 |
| `Country` | string | Yes | 100 |

### Orders Table

| Column | Type | Required | Max Length |
|--------|------|----------|------------|
| `Id` | UUID | Yes (auto) | - |
| `CustomerId` | UUID (FK) | Yes | - |
| `ProductName` | string | Yes | 200 |
| `Quantity` | int | Yes | - |
| `Price` | decimal | Yes | (18,2) |
| `OrderDate` | DateTime | Yes | - |

### Relationships

- **Customer → Addresses:** One-to-Many (cascade delete)
- **Customer → Orders:** One-to-Many (cascade delete)

---

## API Endpoints

Base URL: `https://customerexcelapi-production.up.railway.app`

---

### POST `/api/customers/import`

Upload an Excel file (.xlsx) to import customers with addresses and orders.

#### Request

- **Content-Type:** `multipart/form-data`
- **Max File Size:** 50 MB

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | File (.xlsx) | Yes | Excel file with customer data |

#### Excel File Format

The Excel file must have a **header row** (row 1) with column names. Column names are **case-insensitive**. All columns are optional - the API handles what's provided.

**Columns mapping:**

Both internal names (`ProductName`) and display names (`Product Name`) are accepted.

| Excel Header | Internal Name | Maps To Table | Maps To Column | Required |
|-------------|---------------|--------------|----------------|----------|
| `Name` | `Name` | Customers | Name | Yes |
| `Email` | `Email` | Customers | Email | Yes |
| `Street` | `Street` | Addresses | Street | No |
| `City` | `City` | Addresses | City | No |
| `Country` | `Country` | Addresses | Country | No |
| `Product Name` | `ProductName` | Orders | ProductName | No |
| `Quantity` | `Quantity` | Orders | Quantity | No |
| `Price` | `Price` | Orders | Price | No |
| `Order Date` | `OrderDate` | Orders | OrderDate | No |

**Example Excel layout:**

| Name | Email | Street | City | Country | Product Name | Quantity | Price | Order Date |
|------|-------|--------|------|---------|-------------|----------|-------|------------|
| Ahmed | ahmed@test.com | 10 Nile St | Cairo | Egypt | Laptop | 2 | 1500.50 | 2026-01-15 |
| Ahmed | ahmed@test.com | 10 Nile St | Cairo | Egypt | Mouse | 5 | 25.00 | 2026-02-20 |
| Sara | sara@test.com | 5 Sea St | Alex | Egypt | Keyboard | 1 | 75.00 | 2026-03-10 |
| Mohamed | mohamed@test.com | 20 Mountain Rd | Giza | Egypt | Monitor | 3 | 500.00 | 2026-04-05 |

**How Import Works:**

- Rows with the same Name+Email are treated as the **same customer** (deduplicated)
- Addresses are deduplicated per customer (same Street+City+Country = one address)
- Orders are deduplicated per customer (same ProductName+Quantity+Price+OrderDate = one order)
- **All deduplication checks the database** — re-importing the same file returns `inserted: 0`
- In the example above: 3 Customers, 3 Addresses, 4 Orders = 10 database records

#### Response

**Status:** `200 OK`

```json
{
  "totalRows": 4,
  "inserted": 7,
  "durationMs": 412
}
```

| Field | Type | Description |
|-------|------|-------------|
| `totalRows` | int | Total rows parsed from Excel |
| `inserted` | int | Total records inserted (customers + addresses + orders) |
| `durationMs` | long | Operation time in milliseconds |

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
  console.log(`Imported ${result.inserted} records in ${result.durationMs}ms`);
  return result;
}

// Usage with file input
document.getElementById('fileInput').addEventListener('change', async (e) => {
  const file = e.target.files[0];
  if (file) {
    const result = await importCustomers(file);
    alert(`Successfully imported ${result.inserted} records!`);
  }
});
```

---

### POST `/api/customers/export`

Export customers as Excel file (.xlsx) with **dynamic column selection**. The frontend can request any combination of columns from any table.

#### Request

- **Content-Type:** `application/json`

```json
{
  "columns": ["Name", "City"]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `columns` | string[] | Yes | Column names to include in export (accepts both internal and display names) |

#### Available Columns

**From Customers:**

| Column Name | Description |
|-------------|-------------|
| `Name` | Customer name |
| `Email` | Customer email |

**From Addresses:**

| Column Name | Description |
|-------------|-------------|
| `Street` | Street address |
| `City` | City name |
| `Country` | Country name |

**From Orders:**

| Column Name | Display Name | Description |
|-------------|-------------|-------------|
| `ProductName` | `Product Name` | Product name |
| `Quantity` | `Quantity` | Order quantity |
| `Price` | `Price` | Unit price |
| `OrderDate` | `Order Date` | Order date (YYYY-MM-DD) |

#### Dynamic Column Selection Examples

The API **automatically JOINs only the required tables** based on requested columns:

```json
// Only customer info → Queries Customers table only
{"columns": ["Name"]}

// Customer + address → Queries Customers + Addresses
{"columns": ["Name", "City"]}

// Customer + orders → Queries Customers + Orders
{"columns": ["Name", "ProductName", "Price"]}

// Everything → Queries all 3 tables
{"columns": ["Name", "Email", "Street", "City", "Country", "ProductName", "Quantity", "Price", "OrderDate"]}
```

| Columns Requested | Tables Joined |
|-------------------|---------------|
| Name, Email | Customers only |
| Name, Email, Street, City, Country | Customers + Addresses |
| Name, Email, ProductName, Quantity, Price, OrderDate | Customers + Orders |
| All columns | Customers + Addresses + Orders |

#### Response

**Status:** `200 OK`

- **Content-Type:** `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- **Content-Disposition:** `attachment; filename=customers.xlsx`
- **Body:** Binary Excel file (.xlsx)

#### cURL Examples

```bash
# Export customer names only
curl -X POST https://customerexcelapi-production.up.railway.app/api/customers/export \
  -H "Content-Type: application/json" \
  -d '{"columns": ["Name"]}' \
  --output names.xlsx

# Export customers with cities
curl -X POST https://customerexcelapi-production.up.railway.app/api/customers/export \
  -H "Content-Type: application/json" \
  -d '{"columns": ["Name", "City"]}' \
  --output customers_cities.xlsx

# Export all data
curl -X POST https://customerexcelapi-production.up.railway.app/api/customers/export \
  -H "Content-Type: application/json" \
  -d '{"columns": ["Name", "Email", "Street", "City", "Country", "ProductName", "Quantity", "Price", "OrderDate"]}' \
  --output full_export.xlsx
```

#### JavaScript Examples

```javascript
// Export only customer names
async function exportNames() {
  const response = await fetch(
    'https://customerexcelapi-production.up.railway.app/api/customers/export',
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ columns: ['Name'] }),
    }
  );

  const blob = await response.blob();
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = 'names.xlsx';
  a.click();
  window.URL.revokeObjectURL(url);
}

// Export customers with their cities
async function exportWithCities() {
  const response = await fetch(
    'https://customerexcelapi-production.up.railway.app/api/customers/export',
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ columns: ['Name', 'Email', 'City'] }),
    }
  );

  const blob = await response.blob();
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = 'customers_with_cities.xlsx';
  a.click();
  window.URL.revokeObjectURL(url);
}
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
        .result { margin-top: 10px; padding: 10px; border-radius: 4px; }
        .error { background: #f8d7da; color: #721c24; }
        .success { background: #d4edda; color: #155724; }
        label { margin-right: 15px; }
    </style>
</head>
<body>
    <h1>Customer Excel Manager</h1>

    <!-- Import Section -->
    <div class="section">
        <h2>Import Customers</h2>
        <p>Upload Excel with columns: Name, Email, Street, City, Country, ProductName, Quantity, Price, OrderDate</p>
        <input type="file" id="fileInput" accept=".xlsx" />
        <button onclick="handleImport()">Import</button>
        <div id="importResult"></div>
    </div>

    <!-- Export Section -->
    <div class="section">
        <h2>Export Customers</h2>
        <p>Select columns to export (any combination from any table):</p>
        <h4>Customers:</h4>
        <label><input type="checkbox" value="Name" class="col-check" checked> Name</label>
        <label><input type="checkbox" value="Email" class="col-check" checked> Email</label>
        <h4>Addresses:</h4>
        <label><input type="checkbox" value="Street" class="col-check"> Street</label>
        <label><input type="checkbox" value="City" class="col-check"> City</label>
        <label><input type="checkbox" value="Country" class="col-check"> Country</label>
        <h4>Orders:</h4>
        <label><input type="checkbox" value="ProductName" class="col-check"> Product Name</label>
        <label><input type="checkbox" value="Quantity" class="col-check"> Quantity</label>
        <label><input type="checkbox" value="Price" class="col-check"> Price</label>
        <label><input type="checkbox" value="OrderDate" class="col-check"> Order Date</label>
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

                if (!response.ok) throw new Error(data.error || 'Import failed');

                resultDiv.className = 'result success';
                resultDiv.textContent = `Imported ${data.inserted} records from ${data.totalRows} rows in ${data.durationMs}ms`;
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
                resultDiv.textContent = `Downloaded with ${columns.length} columns`;
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
import { useState } from 'react';

const API_BASE = 'https://customerexcelapi-production.up.railway.app/api/customers';

const ALL_COLUMNS = [
  { name: 'Name', table: 'Customers' },
  { name: 'Email', table: 'Customers' },
  { name: 'Street', table: 'Addresses' },
  { name: 'City', table: 'Addresses' },
  { name: 'Country', table: 'Addresses' },
  { name: 'ProductName', table: 'Orders' },
  { name: 'Quantity', table: 'Orders' },
  { name: 'Price', table: 'Orders' },
  { name: 'OrderDate', table: 'Orders' },
];

function CustomerManager() {
  const [selected, setSelected] = useState(['Name', 'Email']);
  const [message, setMessage] = useState(null);
  const [loading, setLoading] = useState(false);

  const toggleColumn = (col) => {
    setSelected(prev =>
      prev.includes(col) ? prev.filter(c => c !== col) : [...prev, col]
    );
  };

  const handleImport = async (e) => {
    const file = e.target.files[0];
    if (!file) return;

    setLoading(true);
    const formData = new FormData();
    formData.append('file', file);

    try {
      const res = await fetch(`${API_BASE}/import`, { method: 'POST', body: formData });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error);
      setMessage({ type: 'success', text: `Imported ${data.inserted} records in ${data.durationMs}ms` });
    } catch (err) {
      setMessage({ type: 'error', text: err.message });
    } finally {
      setLoading(false);
    }
  };

  const handleExport = async () => {
    if (selected.length === 0) {
      setMessage({ type: 'error', text: 'Select at least one column' });
      return;
    }

    setLoading(true);
    try {
      const res = await fetch(`${API_BASE}/export`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ columns: selected }),
      });
      if (!res.ok) throw new Error((await res.json()).error);

      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'customers.xlsx';
      a.click();
      URL.revokeObjectURL(url);

      setMessage({ type: 'success', text: `Exported ${selected.length} columns` });
    } catch (err) {
      setMessage({ type: 'error', text: err.message });
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ maxWidth: 600, margin: '40px auto' }}>
      <h2>Import</h2>
      <input type="file" accept=".xlsx" onChange={handleImport} disabled={loading} />

      <h2>Export</h2>
      {ALL_COLUMNS.map(col => (
        <label key={col.name} style={{ marginRight: 15 }}>
          <input
            type="checkbox"
            checked={selected.includes(col.name)}
            onChange={() => toggleColumn(col.name)}
          />
          {col.name} <small>({col.table})</small>
        </label>
      ))}
      <br /><br />
      <button onClick={handleExport} disabled={loading}>
        Export Selected Columns
      </button>

      {message && <p style={{ color: message.type === 'error' ? 'red' : 'green' }}>{message.text}</p>}
    </div>
  );
}

export default CustomerManager;
```

---

## Reminder System

A background service that sends meeting reminders to users on a schedule. Reminders repeat until the user marks them as read or the max retry count is reached.

### How It Works

```
Create Reminder
  → MeetingTime: 20:00, NotifyBefore: 10min, Repeat: 5min, MaxRetry: 12
  → First notification sent at: 19:50
  → If not read → re-sent every 5 minutes
  → Mark as Read → stops sending
  → RetryCount >= MaxRetry → marked as Expired
```

### Notification Reliability

Notifications use a **fallback chain** — if one method fails, the next is tried:

```
SignalR (real-time) → WebPush (browser push) → logged as failed
```

**Key guarantee:** If a notification fails (both SignalR and WebPush), the reminder **continues processing normally**:
- `RetryCount` still increments
- `NextReminderTime` is still scheduled
- When `MaxRetryCount` is reached, it still becomes `Expired`

Notification failure **never** stops the reminder lifecycle.

### Reminder API Endpoints

#### POST `/api/reminders`

Create a new reminder.

```json
{
  "title": "Meeting with client",
  "message": "Discuss project requirements",
  "meetingTime": "2026-08-01T20:00:00Z",
  "notifyBeforeMinutes": 10,
  "repeatEveryMinutes": 5,
  "maxRetryCount": 12
}
```

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `title` | string | required | Reminder title |
| `message` | string | required | Reminder message |
| `meetingTime` | DateTime | required | When the meeting occurs |
| `notifyBeforeMinutes` | int | 10 | Minutes before meeting to start notifying |
| `repeatEveryMinutes` | int | 5 | Minutes between each retry |
| `maxRetryCount` | int | 12 | Max notifications before marking as expired |

**Response:** `201 Created`

#### GET `/api/reminders`

Get all reminders for the current user.

**Query params:** `?status=Pending|Read|Expired|Cancelled`

**Response:** `200 OK`

```json
{
  "reminders": [...],
  "totalCount": 5
}
```

#### GET `/api/reminders/{id}`

Get a single reminder by ID.

#### PATCH `/api/reminders/{id}/read`

Mark a reminder as read. Stops all future notifications for this reminder.

**Response:** `200 OK` or `400` if already read/expired

#### DELETE `/api/reminders/{id}`

Cancel a pending reminder.

**Response:** `204 No Content`

### Reminder Status Values

| Status | Meaning | Can Mark Read? | Can Cancel? |
|--------|---------|----------------|-------------|
| `Pending` | Active, sending notifications on schedule | Yes | Yes |
| `Read` | User marked as read, notifications stopped | No (`400: "Reminder is already Read"`) | No |
| `Expired` | Max retry count reached, notifications stopped | No (`400: "Reminder is already Expired"`) | No |
| `Cancelled` | User cancelled the reminder | No | No |

### Error Responses

| Status | Error | When |
|--------|-------|------|
| `400` | `"Reminder is already Read"` | Mark as Read on Read reminder |
| `400` | `"Reminder is already Expired"` | Mark as Read on Expired reminder |
| `400` | `"Reminder is already Cancelled"` | Mark as Read on Cancelled reminder |
| `400` | `"Reminder is not in Pending status"` | Cancel a non-Pending reminder |
| `404` | Not Found | Reminder doesn't exist or belongs to another user |

### User Isolation

Each user can only access their own reminders. The `X-User-Id` header is used to identify the user. If you try to access a reminder belonging to another user, you'll get a `404 Not Found` response.

---

## Real-Time Notifications

The API delivers reminder notifications through two channels:

### SignalR (In-App Real-Time)

| Setting | Value |
|---------|-------|
| Hub URL | `wss://customerexcelapi-production.up.railway.app/hubs/notifications` |
| Auth | `X-User-Id` header |
| Event | `ReminderNotification` |

Payload:
```json
{
  "type": "reminder",
  "reminderId": "uuid",
  "title": "Meeting with client",
  "body": "Discuss project requirements",
  "meetingTime": "2026-08-01T20:00:00Z"
}
```

### WebPush (Browser Notifications)

| Setting | Value |
|---------|-------|
| Subscription endpoint | `POST /api/push-subscriptions` |
| Unsubscribe endpoint | `DELETE /api/push-subscriptions?endpoint=...` |
| VAPID public key | `BIBOwUuD3kOdCfI3yx5JPy-bHUhx76C5KZPnloSv_MKBIM0Exey3ZT77Km42DOsqNWn6wlvj_PtulMOyNYmNyAs` |

For full frontend integration code, see **FRONTEND.md**.

---

## CORS

The API supports **cross-origin requests** from any frontend. No special configuration is needed.

| Setting | Value |
|---------|-------|
| `Access-Control-Allow-Origin` | `*` |
| `Access-Control-Allow-Methods` | `*` (all methods) |
| `Access-Control-Allow-Headers` | `*` (all headers) |
| `Access-Control-Expose-Headers` | `Content-Disposition` |

- **Preflight (OPTIONS)** requests are handled automatically by the server
- **Any origin** is allowed (localhost, production domains, etc.)
- **Any HTTP method** is allowed (GET, POST, OPTIONS)
- **Any request header** is allowed (Content-Type, Authorization, etc.)
- **`Content-Disposition`** header is exposed so frontends can read the download filename

---

## Data Persistence

Data persists across server restarts and deployments. The API uses EF Core migrations (`Database.Migrate()`) which only creates tables if they don't exist — **no data is ever dropped**.

---

## Error Handling

| Status | Meaning | Example |
|--------|---------|---------|
| `200` | Success | Import/Export completed |
| `400` | Bad request | No file uploaded |
| `500` | Server error | DB connection issue, corrupt file |

Error response format:

```json
{
  "error": "Primary error message",
  "inner": "Inner exception message (if any)"
}
```

---

## Project Structure

```
CustomerExcelApi/
├── Controllers/
│   ├── CustomersController.cs
│   ├── PushSubscriptionsController.cs
│   └── RemindersController.cs
├── Data/
│   ├── AppDbContext.cs
│   └── Configurations/
│       ├── CustomerConfiguration.cs
│       ├── PushSubscriptionConfiguration.cs
│       └── ReminderConfiguration.cs
├── Entities/
│   ├── Customer.cs
│   ├── Address.cs
│   ├── Order.cs
│   ├── PushSubscription.cs
│   └── Reminder.cs
├── Features/
│   ├── Customers/
│   │   ├── Commands/ImportCustomers/
│   │   ├── DTOs/
│   │   └── Queries/ExportCustomers/
│   └── Reminders/
│       └── DTOs/
├── Hubs/
│   └── NotificationHub.cs
├── Interfaces/
├── Repositories/
│   ├── CustomerBulkRepository.cs
│   └── CustomerReadRepository.cs
├── Services/
│   ├── ExcelService.cs
│   ├── ReminderBackgroundService.cs
│   └── Notifications/
│       ├── INotificationService.cs
│       ├── NotificationService.cs
│       ├── SignalRNotificationProvider.cs
│       └── WebPushNotificationProvider.cs
├── Migrations/
├── Dockerfile
└── Program.cs
```

---

## Local Development

```bash
git clone https://github.com/Mohamed-ehab-mohy/CustomerExcelApi.git
cd CustomerExcelApi
dotnet run

# Swagger: http://localhost:5247/swagger
```

Requires `appsettings.Development.json` with your local database connection string (not committed to git).

---

## License

MIT
