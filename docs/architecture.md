# ScripOx — Architecture & Setup Guide
**Ox Tech — oxtech.uk**

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────┐
│                        ScripOx System                           │
│                   Ox Tech — oxtech.uk                           │
└──────────────────────────────────────────────────────────────────┘

        ┌───────────────────────────────────┐
        │       C# WPF Desktop App          │
        │  (.NET 8 • MVVM • CommunityToolkit)│
        │                                   │
        │  Login → Dashboard → Companies    │
        │  Raw Records → Map → Exports      │
        │  Notes • Comments • Status        │
        │  Arabic/English RTL/LTR           │
        └────────┬──────────────┬───────────┘
                 │              │
        HTTP API │     Direct   │ MySQL
        (FastAPI)│              │ Connection
                 ▼              ▼
   ┌─────────────────────┐  ┌───────────────────────────────────┐
   │  Python FastAPI      │  │         MySQL 8 Database          │
   │  Extractor Service   │  │                                   │
   │                      │  │  users / roles                    │
   │  /api/jobs     POST  │  │  sources / field_mappings         │
   │  /api/jobs/{id} GET  │  │  extraction_jobs / raw_records    │
   │  /api/health   GET   │  │  companies (POINT SRID 4326)      │
   │                      │  │  company_statuses / tags          │
   │  Connectors:         │  │  company_notes / comments / links │
   │   ├ google_maps      │  │  export_jobs / translations       │
   │   └ csv_file         │  │  audit_logs                       │
   │                      │  └───────────────────────────────────┘
   │  Pipelines:          │
   │   ├ cleaning          │
   │   └ deduplication    │
   └─────────────────────┘
```

---

## Quick Start

### 1. Database Setup
```bash
mysql -u root -p < ScripOx.Database/schema.sql
mysql -u root -p scripox_db < ScripOx.Database/seeds/roles.sql
mysql -u root -p scripox_db < ScripOx.Database/seeds/statuses.sql
mysql -u root -p scripox_db < ScripOx.Database/seeds/translations.sql
```

Default admin user: `superadmin` / `Admin@ScripOx2025`

---

### 2. Python Extractor
```bash
cd ScripOx.Extractor
python -m venv .venv
.venv\Scripts\activate          # Windows
pip install -r requirements.txt
copy .env.example .env          # Fill in DB credentials
uvicorn main:app --host 127.0.0.1 --port 8765
```

Test: http://127.0.0.1:8765/docs

---

### 3. C# Desktop App
```bash
cd ScripOx.Desktop
# Set environment variables (or edit AppSettings.cs):
set DB_HOST=localhost
set DB_USER=root
set DB_PASSWORD=yourpassword
set DB_NAME=scripox_db

dotnet restore
dotnet run
```

---

## Project Structure

```
ScripOx/
├── ScripOx.Database/
│   ├── schema.sql              ← 16 tables
│   ├── migrations/             ← 001 initial, 002 indexes
│   └── seeds/                  ← roles, statuses, translations
│
├── ScripOx.Extractor/          ← Python FastAPI
│   ├── api/                    ← HTTP endpoints
│   ├── connectors/             ← Data source connectors
│   ├── pipelines/              ← Cleaning + Deduplication
│   ├── normalizers/            ← Phone + Geocoding
│   ├── jobs/                   ← Job runner orchestrator
│   ├── dto/                    ← Pydantic schemas
│   ├── db/                     ← SQLAlchemy async engine
│   ├── core/                   ← Config + settings
│   └── main.py
│
├── ScripOx.Desktop/            ← C# WPF (.NET 8)
│   ├── Views/                  ← XAML windows
│   ├── ViewModels/             ← MVVM ViewModels
│   ├── Models/                 ← Domain models
│   ├── Services/               ← Business logic + DB access
│   ├── Helpers/                ← RelayCommand, RTL, etc.
│   ├── Resources/
│   │   ├── map/map.html        ← Leaflet.js map
│   │   ├── Themes/             ← Dark/Light XAML themes
│   │   └── i18n/              ← en.resx / ar.resx
│   └── App.xaml
│
└── docs/
    ├── architecture.md         ← This file
    ├── erd.md                  ← Database ERD
    └── api-spec.md             ← FastAPI endpoint reference
```

---

## Technology Decisions

| Component | Technology | Why |
|-----------|------------|-----|
| Desktop UI | C# WPF .NET 8 | Native Windows, rich DataGrid, WebView2 |
| UI Pattern | MVVM + CommunityToolkit | Testable, maintainable |
| Extractor | Python FastAPI | Async, rich scraping ecosystem |
| Database | MySQL 8 | Spatial POINT/SRID 4326 support |
| Excel | ClosedXML | Best .NET Excel library, no Office needed |
| Map | Leaflet.js + WebView2 | Flexible, dark tiles, cluster |
| Logging | Serilog (C#) + Loguru (Python) | Structured, file rotation |
| Password | BCrypt (workFactor 12) | Industry standard |
| Geocoding | Nominatim (free) / Google (paid) | Configurable |
| Deduplication | Fuzzy name + exact phone/email/domain | Multi-signal |

---

## Adding a New Connector

1. Create `ScripOx.Extractor/connectors/my_connector.py`
2. Inherit from `BaseConnector`
3. Implement `get_name()` and `fetch()`
4. Register in `CONNECTOR_MAP` in `jobs/job_runner.py`
5. Add the source record in the database with `connector='my_connector'`
