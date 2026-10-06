# ScripOx API Specification
**Python FastAPI Extractor — Base URL: `http://127.0.0.1:8765`**

---

## Endpoints

### Health

#### `GET /api/health`
```json
{
  "status": "ok",
  "version": "1.0.0",
  "db": "ok"
}
```

---

### Jobs

#### `POST /api/jobs` — Start extraction job

**Request:**
```json
{
  "source_id": 1,
  "triggered_by": 1,
  "params": {
    "keyword": "restaurant",
    "location": "London, UK",
    "limit": 100
  }
}
```

**Response (201):**
```json
{
  "id": 42,
  "source_id": 1,
  "status": "pending",
  "total_found": 0,
  "total_saved": 0,
  "total_dupes": 0,
  "total_errors": 0,
  "progress_pct": 0,
  "started_at": null,
  "ended_at": null,
  "error_msg": null,
  "created_at": "2025-01-15T10:30:00"
}
```

---

#### `GET /api/jobs` — List jobs (paginated)

**Query params:**
- `page` (default: 1)
- `page_size` (default: 20)

**Response (200):** Array of job objects.

---

#### `GET /api/jobs/{id}` — Get job status

**Response (200):** Single job object (same shape as above).

**Status values:**
| Value | Meaning |
|-------|---------|
| `pending` | Queued, not started |
| `running` | In progress |
| `completed` | Finished successfully |
| `failed` | Error occurred |
| `cancelled` | Manually cancelled |

---

#### `DELETE /api/jobs/{id}` — Cancel job

**Response:** 204 No Content

---

## Connector Params Reference

### `google_maps` connector
| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `keyword` | string | ✅ | Search term (e.g. "restaurant") |
| `location` | string | ✅ | City/address (e.g. "London, UK") |
| `limit` | int | ✗ | Max results (default: 60, max: 120) |
| `api_key` | string | ✗ | Overrides settings.GOOGLE_MAPS_API_KEY |

### `csv_file` connector
| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `file_path` | string | ✅ | Absolute path to .csv or .xlsx |
| `delimiter` | string | ✗ | CSV delimiter (default: `,`) |
| `encoding` | string | ✗ | File encoding (default: `utf-8-sig`) |
| `column_map` | object | ✗ | `{"Source Col": "target_field"}` |

---

## Error Responses

All errors follow this format:
```json
{
  "detail": "Error message here"
}
```

| Code | Meaning |
|------|---------|
| 400 | Bad request (invalid params) |
| 404 | Resource not found |
| 422 | Validation error |
| 500 | Internal server error |
