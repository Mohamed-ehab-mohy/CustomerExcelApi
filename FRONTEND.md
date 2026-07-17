# Frontend Integration Guide - CustomerExcelApi

Complete documentation for frontend developers integrating with the Customer Excel API.

**Base URL:** `https://customerexcelapi-production.up.railway.app`

**Swagger (interactive docs):** `https://customerexcelapi-production.up.railway.app/swagger/index.html`

---

## Table of Contents

1. [Quick Start](#quick-start)
2. [API Overview](#api-overview)
3. [Import Endpoint](#import-endpoint)
4. [Export Endpoint](#export-endpoint)
5. [Database Schema](#database-schema)
6. [Available Columns](#available-columns)
7. [Column Selection Logic](#column-selection-logic)
8. [Error Handling](#error-handling)
9. [Code Examples](#code-examples)
10. [FAQ](#faq)

---

## Quick Start

### 5-Minute Integration

```html
<input type="file" id="fileInput" accept=".xlsx" />
<button onclick="importFile()">Import</button>
<button onclick="exportFile()">Export</button>

<script>
const API = 'https://customerexcelapi-production.up.railway.app/api/customers';

async function importFile() {
  const file = document.getElementById('fileInput').files[0];
  const form = new FormData();
  form.append('file', file);

  const res = await fetch(`${API}/import`, { method: 'POST', body: form });
  const data = await res.json();
  console.log(`Imported ${data.inserted} records in ${data.durationMs}ms`);
}

async function exportFile() {
  const res = await fetch(`${API}/export`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ columns: ['Name', 'Email'] })
  });

  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = 'customers.xlsx';
  a.click();
  URL.revokeObjectURL(url);
}
</script>
```

---

## API Overview

| Method | Endpoint | Content-Type | Description |
|--------|----------|-------------|-------------|
| `POST` | `/api/customers/import` | `multipart/form-data` | Upload Excel file to import data |
| `POST` | `/api/customers/export` | `application/json` | Export data as Excel file |

### Common Headers

**Import (no extra headers needed - browser handles multipart):**
```
Content-Type: multipart/form-data (auto-set by browser)
```

**Export:**
```
Content-Type: application/json
```

---

## CORS

The API supports **cross-origin requests** from any frontend. No special configuration is needed.

- **Preflight (OPTIONS)** requests are handled automatically by the server
- **Any origin** is allowed (localhost, production domains, etc.)
- **Any HTTP method** is allowed (GET, POST, OPTIONS)
- **Any request header** is allowed (Content-Type, Authorization, etc.)
- **`Content-Disposition`** header is exposed for download filename access

This means you can call the API from any frontend (localhost, Vercel, Netlify, etc.) without CORS issues.

---

## Import Endpoint

### `POST /api/customers/import`

Upload an Excel file to import customers, addresses, and orders into the database.

### Request Format

| Field | Type | Required | Max Size |
|-------|------|----------|----------|
| `file` | File (.xlsx) | Yes | 50 MB |

### JavaScript

```javascript
async function importExcel(file) {
  const formData = new FormData();
  formData.append('file', file, file.name);

  const response = await fetch(
    'https://customerexcelapi-production.up.railway.app/api/customers/import',
    {
      method: 'POST',
      body: formData,
      // Do NOT set Content-Type header - browser sets it automatically with boundary
    }
  );

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.error);
  }

  return await response.json();
}
```

### cURL

```bash
curl -X POST \
  https://customerexcelapi-production.up.railway.app/api/customers/import \
  -F "file=@/path/to/customers.xlsx"
```

### Response (200 OK)

```json
{
  "totalRows": 4,
  "inserted": 7,
  "durationMs": 412
}
```

| Field | Type | Description |
|-------|------|-------------|
| `totalRows` | number | Rows read from the Excel file |
| `inserted` | number | Total database records created (customers + addresses + orders) |
| `durationMs` | number | Server processing time in milliseconds |

### How Import Works

The API parses each Excel row and distributes data across 3 tables:

```
Excel Row
   │
   ├── Name, Email ──────> INSERT INTO Customers (deduplicated by Name+Email)
   │
   ├── Street, City, Country ──> INSERT INTO Addresses (deduplicated per customer)
   │
   └── ProductName, Quantity, Price, OrderDate ──> INSERT INTO Orders (one per row)
```

**Deduplication Rules:**
- Same `Name + Email` = same customer (only 1 record created, even across multiple imports)
- Same customer + same `Street + City + Country` = same address (only 1 record created)
- Same customer + same `ProductName + Quantity + Price + OrderDate` = same order (only 1 record created)
- **All deduplication checks the database** — re-importing the same file returns `inserted: 0`

**Example:**

| Name | Email | City | Product | Qty | Price |
|------|-------|------|---------|-----|-------|
| Ahmed | ahmed@test.com | Cairo | Laptop | 2 | 1500 |
| Ahmed | ahmed@test.com | Cairo | Mouse | 5 | 25 |
| Sara | sara@test.com | Alex | Keyboard | 1 | 75 |

**Database result:**
- Customers: 2 (Ahmed, Sara)
- Addresses: 2 (Cairo for Ahmed, Alex for Sara)
- Orders: 3 (Laptop, Mouse, Keyboard)
- **Total inserted: 7**

---

## Export Endpoint

### `POST /api/customers/export`

Export data from the database as an Excel file with **dynamic column selection**.

### Request Format

```json
{
  "columns": ["Name", "Email", "City"]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `columns` | string[] | Yes | List of column names to export |

### JavaScript

```javascript
async function exportExcel(columns) {
  const response = await fetch(
    'https://customerexcelapi-production.up.railway.app/api/customers/export',
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ columns }),
    }
  );

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.error);
  }

  // Convert response to downloadable file
  const blob = await response.blob();
  const url = window.URL.createObjectURL(blob);

  // Create temporary link and trigger download
  const link = document.createElement('a');
  link.href = url;
  link.download = 'customers.xlsx';
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  window.URL.revokeObjectURL(url);
}
```

### cURL

```bash
curl -X POST \
  https://customerexcelapi-production.up.railway.app/api/customers/export \
  -H "Content-Type: application/json" \
  -d '{"columns": ["Name", "Email", "City"]}' \
  --output customers.xlsx
```

### Response (200 OK)

| Header | Value |
|--------|-------|
| Content-Type | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` |
| Content-Disposition | `attachment; filename=customers.xlsx` |

The response body is the **binary Excel file** (not JSON).

### Export Logic Diagram

```
Requested Columns
       │
       ├── Name, Email only
       │   └── Query: SELECT Name, Email FROM Customers
       │
       ├── Name, Street, City
       │   └── Query: SELECT ... FROM Customers LEFT JOIN Addresses
       │
       ├── Name, ProductName, Price
       │   └── Query: SELECT ... FROM Customers LEFT JOIN Orders
       │
       └── All columns
           └── Query: SELECT ... FROM Customers LEFT JOIN Addresses LEFT JOIN Orders
```

---

## Database Schema

### 3 Related Tables

```
┌──────────────┐
│  CUSTOMERS   │
├──────────────┤
│ Id (UUID)    │──── PK
│ Name         │
│ Email        │
└──────┬───────┘
       │
       ├────────────────────┐
       │                    │
       ▼                    ▼
┌──────────────┐    ┌──────────────┐
│  ADDRESSES   │    │    ORDERS    │
├──────────────┤    ├──────────────┤
│ Id (UUID)    │    │ Id (UUID)    │
│ CustomerId   │    │ CustomerId   │
│ Street       │    │ ProductName  │
│ City         │    │ Quantity     │
│ Country      │    │ Price        │
└──────────────┘    │ OrderDate    │
                    └──────────────┘
```

### Table: Customers

| Column | Type | DB Type | Max Length | Nullable |
|--------|------|---------|-----------|----------|
| `Id` | UUID | uuid | - | No (auto-generated) |
| `Name` | string | varchar | 200 | No |
| `Email` | string | varchar | 200 | No |

### Table: Addresses

| Column | Type | DB Type | Max Length | Nullable |
|--------|------|---------|-----------|----------|
| `Id` | UUID | uuid | - | No (auto-generated) |
| `CustomerId` | UUID | uuid | - | No (FK → Customers) |
| `Street` | string | varchar | 300 | No |
| `City` | string | varchar | 100 | No |
| `Country` | string | varchar | 100 | No |

### Table: Orders

| Column | Type | DB Type | Max Length | Nullable |
|--------|------|---------|-----------|----------|
| `Id` | UUID | uuid | - | No (auto-generated) |
| `CustomerId` | UUID | uuid | - | No (FK → Customers) |
| `ProductName` | string | varchar | 200 | No |
| `Quantity` | integer | int | - | No |
| `Price` | decimal | numeric(18,2) | - | No |
| `OrderDate` | DateTime | date | - | No |

### Relationships

- Each Customer can have **many** Addresses
- Each Customer can have **many** Orders
- Deleting a Customer **cascades** to delete all their Addresses and Orders

---

## Available Columns

### Complete Column Reference

| Column Name | Table | Type | Import Required | Export Available |
|-------------|-------|------|-----------------|------------------|
| `Name` | Customers | string | Yes | Yes |
| `Email` | Customers | string | Yes | Yes |
| `Street` | Addresses | string | No | Yes |
| `City` | Addresses | string | No | Yes |
| `Country` | Addresses | string | No | Yes |
| `ProductName` | Orders | string | No | Yes |
| `Quantity` | Orders | integer | No | Yes |
| `Price` | Orders | decimal | No | Yes |
| `OrderDate` | Orders | date | No | Yes |

### Column Name Rules

- **Case-insensitive**: `name`, `NAME`, `Name` all work
- **Display names accepted**: `Product Name` and `ProductName` both work (same for `Order Date` / `OrderDate`)
- **Spaces in display**: `ProductName` in API = `Product Name` in Excel header
- **Date format**: `OrderDate` accepts `YYYY-MM-DD` format
- **Invalid columns are silently ignored** in export

---

## Column Selection Logic

### What Happens with Different Column Combinations

| Requested Columns | Tables Joined | SQL Effect |
|-------------------|---------------|------------|
| `["Name"]` | Customers only | Simple `SELECT` |
| `["Name", "Email"]` | Customers only | Simple `SELECT` |
| `["Name", "City"]` | Customers + Addresses | LEFT JOIN Addresses |
| `["Name", "Street", "Country"]` | Customers + Addresses | LEFT JOIN Addresses |
| `["Name", "ProductName"]` | Customers + Orders | LEFT JOIN Orders |
| `["Name", "Price", "Quantity"]` | Customers + Orders | LEFT JOIN Orders |
| `["Name", "City", "ProductName"]` | All 3 tables | LEFT JOIN Addresses + Orders |
| `["Name", "Email", "Street", "City", "Country", "ProductName", "Quantity", "Price", "OrderDate"]` | All 3 tables | Full LEFT JOIN |

### Smart Table Detection

The API automatically determines which tables to JOIN:

```
Requested columns analyzed:
  - "Name" → Customer table (always)
  - "Email" → Customer table (always)
  - "Street" → Needs Addresses table
  - "City" → Needs Addresses table
  - "Country" → Needs Addresses table
  - "ProductName" → Needs Orders table
  - "Quantity" → Needs Orders table
  - "Price" → Needs Orders table
  - "OrderDate" → Needs Orders table
```

**If no address or order columns are requested, those tables are NOT queried at all.**

### Excel Export Behavior

The exported Excel file:
- Has a **header row** with display names (bold)
- Contains only the **requested columns**
- Auto-adjusts column widths
- Numbers are formatted (Price shows 2 decimal places)
- Dates show `YYYY-MM-DD` format

---

## Error Handling

### Error Response Format

All errors return JSON:

```json
{
  "error": "Human-readable error message",
  "inner": "Technical details (optional)"
}
```

### Error Codes Reference

#### Import Errors

| HTTP Status | Error Message | Cause | Frontend Action |
|-------------|--------------|-------|-----------------|
| `400` | `"No file uploaded."` | No file or empty file | Show "Please select a file" |
| `500` | `"File contains corrupted data."` | Invalid .xlsx file | Show "Invalid Excel file" |
| `500` | `"relation \"Customers\" does not exist"` | DB not initialized | Retry after a few seconds |
| `500` | `"Connection refused"` | DB connection issue | Retry later |

#### Export Errors

| HTTP Status | Error Message | Cause | Frontend Action |
|-------------|--------------|-------|-----------------|
| `500` | Any DB error | Database issue | Retry later |
| `500` | `"No columns specified"` | Empty columns array | Validate before sending |

### Error Handling Pattern

```javascript
async function safeApiCall(fn) {
  try {
    const result = await fn();
    return { success: true, data: result };
  } catch (err) {
    if (err.message.includes('Failed to fetch')) {
      return { success: false, error: 'Network error - check your connection' };
    }
    return { success: false, error: err.message };
  }
}

// Usage
const importResult = await safeApiCall(() => importExcel(file));
if (importResult.success) {
  showSuccess(`Imported ${importResult.data.inserted} records`);
} else {
  showError(importResult.error);
}
```

---

## Code Examples

### Vanilla JavaScript

#### Import with Progress Feedback

```html
<!DOCTYPE html>
<html>
<body>
  <input type="file" id="fileInput" accept=".xlsx" />
  <button onclick="doImport()">Import</button>
  <div id="status"></div>

  <script>
    const API = 'https://customerexcelapi-production.up.railway.app/api/customers';

    async function doImport() {
      const file = document.getElementById('fileInput').files[0];
      const status = document.getElementById('status');

      if (!file) {
        status.textContent = 'Select a file first!';
        status.style.color = 'red';
        return;
      }

      status.textContent = 'Uploading...';
      status.style.color = 'orange';

      const form = new FormData();
      form.append('file', file);

      try {
        const res = await fetch(`${API}/import`, { method: 'POST', body: form });
        const data = await res.json();

        if (!res.ok) throw new Error(data.error);

        status.textContent = `Success! ${data.inserted} records in ${data.durationMs}ms`;
        status.style.color = 'green';
      } catch (err) {
        status.textContent = `Error: ${err.message}`;
        status.style.color = 'red';
      }
    }
  </script>
</body>
</html>
```

#### Export with Column Picker

```html
<!DOCTYPE html>
<html>
<body>
  <div>
    <h3>Select Export Columns</h3>

    <fieldset>
      <legend>Customers</legend>
      <label><input type="checkbox" class="col" value="Name" checked> Name</label>
      <label><input type="checkbox" class="col" value="Email" checked> Email</label>
    </fieldset>

    <fieldset>
      <legend>Addresses</legend>
      <label><input type="checkbox" class="col" value="Street"> Street</label>
      <label><input type="checkbox" class="col" value="City"> City</label>
      <label><input type="checkbox" class="col" value="Country"> Country</label>
    </fieldset>

    <fieldset>
      <legend>Orders</legend>
      <label><input type="checkbox" class="col" value="ProductName"> Product Name</label>
      <label><input type="checkbox" class="col" value="Quantity"> Quantity</label>
      <label><input type="checkbox" class="col" value="Price"> Price</label>
      <label><input type="checkbox" class="col" value="OrderDate"> Order Date</label>
    </fieldset>

    <button onclick="doExport()">Download Excel</button>
    <div id="status"></div>
  </div>

  <script>
    const API = 'https://customerexcelapi-production.up.railway.app/api/customers';

    async function doExport() {
      const checkboxes = document.querySelectorAll('.col:checked');
      const columns = [...checkboxes].map(cb => cb.value);
      const status = document.getElementById('status');

      if (!columns.length) {
        status.textContent = 'Select at least one column!';
        return;
      }

      status.textContent = 'Exporting...';

      try {
        const res = await fetch(`${API}/export`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ columns })
        });

        if (!res.ok) {
          const err = await res.json();
          throw new Error(err.error);
        }

        const blob = await res.blob();
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'customers.xlsx';
        a.click();
        URL.revokeObjectURL(url);

        status.textContent = `Downloaded with ${columns.length} columns`;
      } catch (err) {
        status.textContent = `Error: ${err.message}`;
      }
    }
  </script>
</body>
</html>
```

### React

#### Full Component with Import + Export

```jsx
import { useState, useRef } from 'react';

const API = 'https://customerexcelapi-production.up.railway.app/api/customers';

const ALL_COLUMNS = {
  Customers: ['Name', 'Email'],
  Addresses: ['Street', 'City', 'Country'],
  Orders: ['ProductName', 'Quantity', 'Price', 'OrderDate'],
};

function CustomerExcelManager() {
  const [selectedCols, setSelectedCols] = useState(['Name', 'Email']);
  const [importResult, setImportResult] = useState(null);
  const [exporting, setExporting] = useState(false);
  const [importing, setImporting] = useState(false);
  const fileRef = useRef(null);

  // ===== IMPORT =====
  const handleImport = async () => {
    const file = fileRef.current?.files?.[0];
    if (!file) return alert('Select a file first');

    setImporting(true);
    setImportResult(null);

    try {
      const form = new FormData();
      form.append('file', file);

      const res = await fetch(`${API}/import`, { method: 'POST', body: form });
      const data = await res.json();

      if (!res.ok) throw new Error(data.error);
      setImportResult(data);
    } catch (err) {
      setImportResult({ error: err.message });
    } finally {
      setImporting(false);
    }
  };

  // ===== EXPORT =====
  const handleExport = async () => {
    if (!selectedCols.length) return alert('Select at least one column');

    setExporting(true);
    try {
      const res = await fetch(`${API}/export`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ columns: selectedCols }),
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
      alert(`Export failed: ${err.message}`);
    } finally {
      setExporting(false);
    }
  };

  // ===== COLUMN TOGGLE =====
  const toggleCol = (col) => {
    setSelectedCols((prev) =>
      prev.includes(col) ? prev.filter((c) => c !== col) : [...prev, col]
    );
  };

  const toggleTable = (tableName) => {
    const cols = ALL_COLUMNS[tableName];
    const allSelected = cols.every((c) => selectedCols.includes(c));

    if (allSelected) {
      setSelectedCols((prev) => prev.filter((c) => !cols.includes(c)));
    } else {
      setSelectedCols((prev) => [...new Set([...prev, ...cols])]);
    }
  };

  return (
    <div style={{ padding: 20, maxWidth: 600, margin: '0 auto' }}>
      <h1>Customer Excel Manager</h1>

      {/* IMPORT */}
      <section style={{ marginBottom: 30, padding: 20, border: '1px solid #ddd', borderRadius: 8 }}>
        <h2>Import</h2>
        <input type="file" accept=".xlsx" ref={fileRef} />
        <button onClick={handleImport} disabled={importing} style={{ marginLeft: 10 }}>
          {importing ? 'Importing...' : 'Import'}
        </button>
        {importResult && !importResult.error && (
          <p style={{ color: 'green' }}>
            Imported {importResult.inserted} records from {importResult.totalRows} rows
            in {importResult.durationMs}ms
          </p>
        )}
        {importResult?.error && (
          <p style={{ color: 'red' }}>Error: {importResult.error}</p>
        )}
      </section>

      {/* EXPORT */}
      <section style={{ padding: 20, border: '1px solid #ddd', borderRadius: 8 }}>
        <h2>Export</h2>
        {Object.entries(ALL_COLUMNS).map(([table, cols]) => (
          <div key={table} style={{ marginBottom: 10 }}>
            <label style={{ fontWeight: 'bold' }}>
              <input
                type="checkbox"
                checked={cols.every((c) => selectedCols.includes(c))}
                onChange={() => toggleTable(table)}
              />{' '}
              {table}
            </label>
            <div style={{ paddingLeft: 20 }}>
              {cols.map((col) => (
                <label key={col} style={{ marginRight: 15 }}>
                  <input
                    type="checkbox"
                    checked={selectedCols.includes(col)}
                    onChange={() => toggleCol(col)}
                  />{' '}
                  {col}
                </label>
              ))}
            </div>
          </div>
        ))}
        <button onClick={handleExport} disabled={exporting} style={{ marginTop: 10 }}>
          {exporting ? 'Exporting...' : 'Download Excel'}
        </button>
      </section>
    </div>
  );
}

export default CustomerExcelManager;
```

### Vue.js

```vue
<template>
  <div>
    <h1>Customer Excel Manager</h1>

    <!-- Import -->
    <div>
      <h2>Import</h2>
      <input type="file" ref="fileInput" accept=".xlsx" />
      <button @click="importFile" :disabled="importing">
        {{ importing ? 'Importing...' : 'Import' }}
      </button>
      <p v-if="importResult" :style="{ color: importResult.error ? 'red' : 'green' }">
        {{ importResult.error || `Imported ${importResult.inserted} records` }}
      </p>
    </div>

    <!-- Export -->
    <div>
      <h2>Export</h2>
      <div v-for="(cols, table) in allColumns" :key="table">
        <strong>{{ table }}</strong>
        <label v-for="col in cols" :key="col" style="margin-right: 15px">
          <input type="checkbox" :value="col" v-model="selected" />
          {{ col }}
        </label>
      </div>
      <button @click="exportFile" :disabled="exporting">
        {{ exporting ? 'Exporting...' : 'Download' }}
      </button>
    </div>
  </div>
</template>

<script>
const API = 'https://customerexcelapi-production.up.railway.app/api/customers';

export default {
  data: () => ({
    selected: ['Name', 'Email'],
    importing: false,
    exporting: false,
    importResult: null,
    allColumns: {
      Customers: ['Name', 'Email'],
      Addresses: ['Street', 'City', 'Country'],
      Orders: ['ProductName', 'Quantity', 'Price', 'OrderDate'],
    },
  }),
  methods: {
    async importFile() {
      const file = this.$refs.fileInput.files[0];
      if (!file) return;
      this.importing = true;
      this.importResult = null;

      const form = new FormData();
      form.append('file', file);

      try {
        const res = await fetch(`${API}/import`, { method: 'POST', body: form });
        const data = await res.json();
        this.importResult = res.ok ? data : { error: data.error };
      } catch (e) {
        this.importResult = { error: e.message };
      }
      this.importing = false;
    },
    async exportFile() {
      if (!this.selected.length) return;
      this.exporting = true;

      try {
        const res = await fetch(`${API}/export`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ columns: this.selected }),
        });
        if (!res.ok) throw new Error((await res.json()).error);
        const blob = await res.blob();
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'customers.xlsx';
        a.click();
        URL.revokeObjectURL(url);
      } catch (e) {
        alert(e.message);
      }
      this.exporting = false;
    },
  },
};
</script>
```

### Angular

```typescript
// customer.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class CustomerService {
  private apiUrl = 'https://customerexcelapi-production.up.railway.app/api/customers';

  constructor(private http: HttpClient) {}

  importCustomers(file: File): Observable<any> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post(`${this.apiUrl}/import`, form);
  }

  exportCustomers(columns: string[]): Observable<Blob> {
    return this.http.post(`${this.apiUrl}/export`, { columns }, {
      responseType: 'blob'
    });
  }
}
```

```typescript
// customer.component.ts
import { Component } from '@angular/core';
import { CustomerService } from './customer.service';
import { saveAs } from 'file-saver';

@Component({ selector: 'app-customer', templateUrl: './customer.component.html' })
export class CustomerComponent {
  selectedCols = ['Name', 'Email'];
  allCols = {
    Customers: ['Name', 'Email'],
    Addresses: ['Street', 'City', 'Country'],
    Orders: ['ProductName', 'Quantity', 'Price', 'OrderDate'],
  };

  constructor(private service: CustomerService) {}

  onImport(event: Event) {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.service.importCustomers(file).subscribe({
      next: (res) => alert(`Imported ${res.inserted} records`),
      error: (err) => alert(`Error: ${err.error?.error || err.message}`),
    });
  }

  onExport() {
    this.service.exportCustomers(this.selectedCols).subscribe({
      next: (blob) => saveAs(blob, 'customers.xlsx'),
      error: (err) => alert(`Error: ${err.message}`),
    });
  }

  toggleCol(col: string) {
    const idx = this.selectedCols.indexOf(col);
    idx > -1 ? this.selectedCols.splice(idx, 1) : this.selectedCols.push(col);
  }
}
```

### Axios (Node.js / Browser)

```javascript
import axios from 'axios';

const API = 'https://customerexcelapi-production.up.railway.app/api/customers';

// Import
async function importExcel(filePath) {
  const FormData = (await import('form-data')).default;
  const fs = await import('fs');

  const form = new FormData();
  form.append('file', fs.createReadStream(filePath));

  const { data } = await axios.post(`${API}/import`, form, {
    headers: form.getHeaders(),
  });

  return data;
}

// Export
async function exportExcel(columns) {
  const { data } = await axios.post(
    `${API}/export`,
    { columns },
    { responseType: 'arraybuffer' }
  );

  fs.writeFileSync('customers.xlsx', Buffer.from(data));
}
```

### Python (requests)

```python
import requests

API = 'https://customerexcelapi-production.up.railway.app/api/customers'

# Import
def import_excel(file_path):
    with open(file_path, 'rb') as f:
        files = {'file': ('data.xlsx', f, 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')}
        r = requests.post(f'{API}/import', files=files)
    return r.json()

# Export
def export_excel(columns, output_path):
    r = requests.post(f'{API}/export', json={'columns': columns})
    with open(output_path, 'wb') as f:
        f.write(r.content)
    print(f'Downloaded {len(r.content)} bytes to {output_path}')

# Examples
result = import_excel('customers.xlsx')
print(f"Imported {result['inserted']} records")

export_excel(['Name', 'Email', 'City'], 'output.xlsx')
```

---

## Reminder System

The API includes a meeting reminder system. Users can create reminders that automatically send notifications on a schedule until the user marks them as read.

### How Reminders Work

```
User creates reminder for 20:00 meeting, notify before 10min, repeat every 5min, max 12 retries
  │
  ├── 19:50 → First notification sent
  ├── 19:55 → Second notification (if not read)
  ├── 20:00 → Third notification (if not read)
  ├── ...
  └── After 12 retries → Marked as Expired

OR
  └── User clicks "Mark as Read" → Stops all notifications
```

### Reminder Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/reminders` | Create a reminder |
| `GET` | `/api/reminders` | List all reminders |
| `GET` | `/api/reminders/{id}` | Get single reminder |
| `PATCH` | `/api/reminders/{id}/read` | Mark as read (stops notifications) |
| `DELETE` | `/api/reminders/{id}` | Cancel a pending reminder |

### Create Reminder

```javascript
async function createReminder() {
  const response = await fetch(
    'https://customerexcelapi-production.up.railway.app/api/reminders',
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-User-Id': 'user-uuid' },
      body: JSON.stringify({
        title: 'Meeting with client',
        message: 'Discuss project requirements',
        meetingTime: '2026-08-01T20:00:00Z',
        notifyBeforeMinutes: 10,
        repeatEveryMinutes: 5,
        maxRetryCount: 12,
      }),
    }
  );
  return await response.json();
}
```

### Get All Reminders

```javascript
async function getReminders(status = 'Pending') {
  const response = await fetch(
    `https://customerexcelapi-production.up.railway.app/api/reminders?status=${status}`,
    { headers: { 'X-User-Id': 'user-uuid' } }
  );
  const data = await response.json();
  // data.reminders = [...], data.totalCount = 5
  return data;
}
```

### Mark as Read (Stops Notifications)

```javascript
async function markAsRead(reminderId) {
  const response = await fetch(
    `https://customerexcelapi-production.up.railway.app/api/reminders/${reminderId}/read`,
    {
      method: 'PATCH',
      headers: { 'X-User-Id': 'user-uuid' },
    }
  );
  return await response.json();
}
```

### Reminder Status Values

| Status | Meaning | Can Mark Read? | Can Cancel? |
|--------|---------|----------------|-------------|
| `Pending` | Active, sending notifications | Yes | Yes |
| `Read` | Stopped by user | No (`400: "Reminder is already Read"`) | No |
| `Expired` | Max retries reached | No (`400: "Reminder is already Expired"`) | No |
| `Cancelled` | Cancelled by user | No (`400: "Reminder is already Cancelled"`) | No |

### Reminder Response Format

```json
{
  "id": "uuid",
  "title": "Meeting with client",
  "message": "Discuss project requirements",
  "meetingTime": "2026-08-01T20:00:00Z",
  "nextReminderTime": "2026-08-01T19:55:00Z",
  "retryCount": 1,
  "maxRetryCount": 12,
  "status": "Pending",
  "createdAt": "2026-07-17T10:00:00Z",
  "readAt": null
}
```

### Cancel Reminder

```javascript
async function cancelReminder(reminderId) {
  const response = await fetch(
    `https://customerexcelapi-production.up.railway.app/api/reminders/${reminderId}`,
    {
      method: 'DELETE',
      headers: { 'X-User-Id': 'user-uuid' },
    }
  );
  // Returns 204 No Content on success
  // Returns 400 if reminder is not in Pending status
}
```

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

### Authentication

All reminder endpoints require `X-User-Id` header to identify the user:

```
X-User-Id: 550e8400-e29b-41d4-a716-446655440000
```

---

## Real-Time Notifications (SignalR)

The API uses **SignalR** for real-time push notifications. When a reminder triggers, the server pushes a notification to the connected client instantly.

### Hub Endpoint

```
wss://customerexcelapi-production.up.railway.app/hubs/notifications
```

### Install SignalR Client

```bash
npm install @microsoft/signalr
```

### Connect to the Hub

```javascript
import * as signalR from '@microsoft/signalr';

const connection = new signalR.HubConnectionBuilder()
  .withUrl('https://customerexcelapi-production.up.railway.app/hubs/notifications', {
    headers: { 'X-User-Id': userId }
  })
  .withAutomaticReconnect()
  .build();

// Listen for reminder notifications
connection.on('ReminderNotification', (notification) => {
  console.log('Reminder:', notification);
  // notification = {
  //   type: 'reminder',
  //   reminderId: 'uuid',
  //   title: 'Meeting with client',
  //   body: 'Discuss project requirements',
  //   meetingTime: '2026-08-01T20:00:00Z'
  // }

  // Show browser notification or in-app toast
  new Notification(notification.title, { body: notification.body });
});

// Start connection
await connection.start();
console.log('SignalR connected');
```

### Connection States

| State | Meaning |
|-------|---------|
| `Connected` | Active and receiving notifications |
| `Reconnecting` | Lost connection, trying to reconnect |
| `Disconnected` | Not connected, needs manual restart |

```javascript
connection.onreconnecting(() => console.log('Reconnecting...'));
connection.onreconnected(() => console.log('Reconnected'));
connection.onclose(() => console.log('Disconnected'));
```

---

## Browser Push Notifications (WebPush)

For notifications when the browser tab is closed or the user is on a different page, use **WebPush**.

### How It Works

1. Frontend registers a **Service Worker**
2. Frontend subscribes to **Push API** and gets a subscription object
3. Frontend sends the subscription to `/api/push-subscriptions`
4. Server sends push notifications via the **WebPush protocol**

### Step 1: Register Service Worker

Create `public/sw.js`:

```javascript
self.addEventListener('push', (event) => {
  const data = event.data ? event.data.json() : {};
  event.waitUntil(
    self.registration.showNotification(data.title || 'Reminder', {
      body: data.body || '',
      icon: '/icon.png',
      data: { reminderId: data.reminderId }
    })
  );
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  event.waitUntil(
    clients.openWindow('/')
  );
});
```

In your app's entry point:

```javascript
if ('serviceWorker' in navigator) {
  await navigator.serviceWorker.register('/sw.js');
}
```

### Step 2: Subscribe to Push

```javascript
const vapidPublicKey = 'BIBOwUuD3kOdCfI3yx5JPy-bHUhx76C5KZPnloSv_MKBIM0Exey3ZT77Km42DOsqNWn6wlvj_PtulMOyNYmNyAs';

async function subscribeToPush(userId) {
  const registration = await navigator.serviceWorker.ready;
  const subscription = await registration.pushManager.subscribe({
    userVisibleOnly: true,
    applicationServerKey: vapidPublicKey
  });

  // Send subscription to server
  await fetch('https://customerexcelapi-production.up.railway.app/api/push-subscriptions', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-User-Id': userId
    },
    body: JSON.stringify({
      endpoint: subscription.endpoint,
      keys: {
        p256dh: arrayBufferToBase64(subscription.getKey('p256dh')),
        auth: arrayBufferToBase64(subscription.getKey('auth'))
      }
    })
  });
}

function arrayBufferToBase64(buffer) {
  return btoa(String.fromCharCode(...new Uint8Array(buffer)));
}
```

### Step 3: Unsubscribe (Optional)

```javascript
async function unsubscribeFromPush(userId, endpoint) {
  const registration = await navigator.serviceWorker.ready;
  const subscription = await registration.pushManager.getSubscription();
  if (subscription) {
    await subscription.unsubscribe();
  }

  await fetch(
    `https://customerexcelapi-production.up.railway.app/api/push-subscriptions?endpoint=${encodeURIComponent(endpoint)}`,
    {
      method: 'DELETE',
      headers: { 'X-User-Id': userId }
    }
  );
}
```

### Push Subscription Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/push-subscriptions` | Register a push subscription |
| `DELETE` | `/api/push-subscriptions?endpoint=...` | Remove a subscription |
| `GET` | `/api/push-subscriptions` | List user's subscriptions |

### VAPID Public Key

```
BIBOwUuD3kOdCfI3yx5JPy-bHUhx76C5KZPnloSv_MKBIM0Exey3ZT77Km42DOsqNWn6wlvj_PtulMOyNYmNyAs
```

---

## Complete Notification Setup

For the best experience, implement **both** SignalR and WebPush:

| Scenario | SignalR | WebPush |
|----------|---------|---------|
| Browser tab open | ✅ Real-time | ✅ Fallback |
| Browser tab closed | ❌ | ✅ Push notification |
| Different page | ❌ | ✅ Push notification |
| Mobile (browser) | ✅ WebSocket | ✅ Push notification |

### Recommended Flow

```
Page load
  ├── Connect to SignalR hub
  ├── Register Service Worker
  ├── Subscribe to Push
  └── Subscribe to Push on server

Reminder triggers
  ├── SignalR → in-app toast (if tab is open)
  └── WebPush → browser notification (if tab is closed or as fallback)
```

---

## FAQ

### Q: Can I send an Excel file with only some columns?

**A:** Yes! The API is flexible:
- **Import:** Only `Name` and `Email` are required. All other columns are optional.
- **Export:** Request any combination of columns. The API only queries the needed tables.

### Q: What happens if I import the same file twice?

**A:** All data is **fully deduplicated** — customers, addresses, AND orders. Re-importing the same file returns `inserted: 0` because all records already exist in the database. This is safe — you can import the same file multiple times without creating duplicates.

### Q: What Excel format is supported?

**A:** Only `.xlsx` format (Office Open XML). Old `.xls` format is NOT supported.

### Q: What's the max file size?

**A:** 50 MB.

### Q: Can I import from a Google Sheet?

**A:** Export the Google Sheet as `.xlsx` first, then upload the file.

### Q: What date format should I use for OrderDate?

**A:** Use `YYYY-MM-DD` format (e.g., `2026-01-15`).

### Q: The export file is empty / has no data

**A:** Check that you've imported data first. The export queries the database - if there's no data, the Excel will have headers only.

### Q: How do I handle CORS errors?

**A:** The API is configured with full CORS support. It accepts requests from **any origin**, with **any HTTP method** (GET, POST, OPTIONS, etc.), and **any headers**. The `Content-Disposition` header is also exposed so frontends can read the filename from export responses.

| CORS Setting | Value |
|--------------|-------|
| `Access-Control-Allow-Origin` | `*` |
| `Access-Control-Allow-Methods` | `*` (all methods) |
| `Access-Control-Allow-Headers` | `*` (all headers) |
| `Access-Control-Expose-Headers` | `Content-Disposition` |

**Preflight requests (OPTIONS)** are handled automatically by the server. No special configuration is needed on the frontend.

If you still get CORS errors, check your browser's developer console for the actual error.

### Q: Can I use this API from a mobile app?

**A:** Yes! It's a standard REST API. Use any HTTP client (fetch, axios, retrofit, Alamofire, etc.).

### Q: Does data persist if the server restarts?

**A:** Yes. Data persists across server restarts and deployments. The API uses EF Core migrations which only create tables if they don't exist — no data is ever dropped.
