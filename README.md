# ScripOx

> **Company Data Management Platform** — Ox Tech ([oxtech.uk](https://oxtech.uk))

ScripOx is a professional, modular workspace for extracting, cleaning, reviewing, and managing company data — not just a data collector. Every record goes through a structured pipeline: receive → clean → review → approve → map/export.

---

## Stack

| Layer | Technology |
|-------|-----------|
| Desktop UI | C# WPF (.NET 8) + MVVM |
| Extractor Service | Python 3.12 + FastAPI |
| Database | MySQL 8 (spatial SRID 4326) |
| Map | Leaflet.js + WebView2 |
| Excel Export | ClosedXML |
| Languages | Arabic (RTL) + English (LTR) |

---

## Quick Start

```bash
# 1. Database
mysql -u root -p < ScripOx.Database/schema.sql
mysql -u root -p scripox_db < ScripOx.Database/seeds/roles.sql
mysql -u root -p scripox_db < ScripOx.Database/seeds/statuses.sql
mysql -u root -p scripox_db < ScripOx.Database/seeds/translations.sql

# 2. Python Extractor
cd ScripOx.Extractor
pip install -r requirements.txt
copy .env.example .env   # fill credentials
uvicorn main:app --port 8765

# 3. Desktop App
cd ScripOx.Desktop
dotnet run
```

Default login: `superadmin` / `Admin@ScripOx2025`

---

## Modules

| Module | Description |
|--------|-------------|
| Auth | Login, roles, permissions |
| Sources | Define data sources |
| Extraction Jobs | Run and monitor jobs |
| Raw Records | Review before approval |
| Companies | Main company table |
| Map Explorer | Interactive Leaflet map |
| Notes & Comments | Internal team notes |
| Status Workflow | Per-company status tracking |
| Export Center | Excel / CSV export |
| Audit Logs | Full change history |
| Localization | Arabic + English |

---

## Documentation

- [Architecture Guide](docs/architecture.md)
- [Database ERD](docs/erd.md)
- [API Specification](docs/api-spec.md)

---

© 2025 Ox Tech — [oxtech.uk](https://oxtech.uk)
