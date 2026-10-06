"""
ScripOx — Browser Extract Router
POST /api/connectors/browser-extract

Receives extraction payloads from the ScripOx Visual Extractor Chrome extension
and saves them directly to raw_records (and optionally companies).
"""

from __future__ import annotations
from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy import text
from pydantic import BaseModel, HttpUrl
from typing import Any, Optional
import json
from loguru import logger

from db.database import get_db

router = APIRouter()


# ── Request / Response Models ─────────────────────────────────

class BrowserExtractRequest(BaseModel):
    url:      str
    title:    str
    selector: str
    xpath:    Optional[str] = None
    mapping:  dict[str, str] = {}
    records:  list[dict[str, Any]] = []


class BrowserExtractResponse(BaseModel):
    ok:          bool
    records_saved: int
    message:     str


def normalize_record(rec: dict[str, Any]) -> dict[str, Any]:
    norm = {}
    for k, v in rec.items():
        if not v:
            continue
        k_lower = k.lower().strip()
        val_str = str(v).strip()

        if "name" in k_lower or "title" in k_lower:
            norm["name"] = val_str
        elif "phone" in k_lower or "tel" in k_lower or "mobile" in k_lower:
            norm["phone"] = val_str
        elif "email" in k_lower or "mail" in k_lower:
            norm["email"] = val_str
        elif "website" in k_lower or "site" in k_lower or "web" in k_lower or "url" in k_lower:
            if "google.com/maps" in val_str and not norm.get("website"):
                pass  # avoid setting maps link as company website if it's the raw place URL
            else:
                norm["website"] = val_str
        elif "address" in k_lower or "location" in k_lower or "loc" in k_lower:
            norm["address"] = val_str
        elif "category" in k_lower or "tag" in k_lower or "type" in k_lower:
            norm["category"] = val_str
        elif "lat" in k_lower:
            norm["latitude"] = val_str
        elif "lon" in k_lower or "lng" in k_lower:
            norm["longitude"] = val_str

    # Extract coordinates from URL if present and lat/lng are missing
    for k, v in rec.items():
        if not v:
            continue
        val_str = str(v).strip()
        if "google.com/maps" in val_str:
            import re
            match = re.search(r'@(-?\d+\.\d+),(-?\d+\.\d+)', val_str)
            if match and not norm.get("latitude"):
                norm["latitude"] = match.group(1)
                norm["longitude"] = match.group(2)

    return norm


# ── Endpoint ──────────────────────────────────────────────────

@router.post("/connectors/browser-extract", response_model=BrowserExtractResponse)
async def browser_extract(
    payload: BrowserExtractRequest,
    db: AsyncSession = Depends(get_db),
):
    """
    Receives data from the ScripOx Chrome Extension.
    Creates a job record and saves each record as a raw_record.
    """
    if not payload.records:
        raise HTTPException(status_code=422, detail="No records provided")

    # Parse host name from URL to use as source name
    from urllib.parse import urlparse
    domain = urlparse(payload.url).netloc or "Unknown Website"
    source_name = f"Browser: {domain}"

    # Check if this source script already exists
    source_result = await db.execute(
        text("SELECT id FROM sources WHERE name = :name LIMIT 1"),
        {"name": source_name}
    )
    source_row = source_result.fetchone()

    config_data = {
        "url":      payload.url,
        "title":    payload.title,
        "selector": payload.selector,
        "xpath":    payload.xpath,
        "mapping":  payload.mapping,
    }

    if not source_row:
        # Create a new source (script) in database
        insert_res = await db.execute(
            text("""
                INSERT INTO sources (name, type, base_url, connector, config_json, is_active, created_by, created_at, updated_at)
                VALUES (:name, 'web', :base_url, 'chrome_extension', :config_json, 1, 1, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            """),
            {
                "name":        source_name,
                "base_url":    payload.url,
                "config_json": json.dumps(config_data, ensure_ascii=False),
            }
        )
        await db.commit()
        # Get last inserted source ID safely
        res_source = await db.execute(text("SELECT last_insert_rowid()"))
        source_id = res_source.scalar()
    else:
        source_id = source_row[0]
        # Update config of existing source
        await db.execute(
            text("""
                UPDATE sources
                SET config_json = :config_json, updated_at = CURRENT_TIMESTAMP
                WHERE id = :id
            """),
            {
                "config_json": json.dumps(config_data, ensure_ascii=False),
                "id":          source_id,
            }
        )
        await db.commit()

    # Create a browser-source job record linked to the source
    result = await db.execute(
        text("""
            INSERT INTO extraction_jobs
              (source_id, triggered_by, status, params_json, started_at, ended_at,
               total_found, total_saved, progress_pct)
            VALUES
              (:source_id, 1, 'completed', :params, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP,
               :total, :total, 100)
        """),
        {
            "source_id": source_id,
            "params": json.dumps({
                "url":      payload.url,
                "title":    payload.title,
                "selector": payload.selector,
                "xpath":    payload.xpath,
                "mapping":  payload.mapping,
                "source":   "chrome_extension",
            }),
            "total": len(payload.records),
        }
    )
    await db.commit()
    # Get last inserted job ID safely
    res_job = await db.execute(text("SELECT last_insert_rowid()"))
    job_id = res_job.scalar()

    saved = 0
    for rec in payload.records:
        try:
            await db.execute(
                text("""
                    INSERT INTO raw_records (job_id, raw_json, status)
                    VALUES (:job_id, :raw_json, 'pending')
                """),
                {
                    "job_id":   job_id,
                    "raw_json": json.dumps(rec, ensure_ascii=False),
                }
            )
            saved += 1

            # Normalize the record to support case-insensitive and label keys
            norm_rec = normalize_record(rec)

            # If the record has a 'name' field, save directly to companies too
            if norm_rec.get("name"):
                await db.execute(
                    text("""
                        INSERT OR IGNORE INTO companies
                          (name, phone, email, website, address, category,
                           latitude, longitude,
                           source_name, source_url, status_id, priority, created_at)
                        VALUES
                          (:name, :phone, :email, :website, :address, :category,
                           :latitude, :longitude,
                           :source_name, :source_url, 1, 'medium', CURRENT_TIMESTAMP)
                    """),
                    {
                        "name":        norm_rec.get("name"),
                        "phone":       norm_rec.get("phone"),
                        "email":       norm_rec.get("email"),
                        "website":     norm_rec.get("website"),
                        "address":     norm_rec.get("address"),
                        "category":    norm_rec.get("category"),
                        "latitude":    float(norm_rec.get("latitude")) if norm_rec.get("latitude") else None,
                        "longitude":   float(norm_rec.get("longitude")) if norm_rec.get("longitude") else None,
                        "source_name": "Chrome Extension",
                        "source_url":  payload.url,
                    }
                )
        except Exception as exc:
            logger.warning(f"Could not save browser record: {exc}")

    await db.commit()

    logger.info(
        f"Browser extract: job={job_id}, url='{payload.url}', "
        f"records={len(payload.records)}, saved={saved}"
    )

    return BrowserExtractResponse(
        ok=True,
        records_saved=saved,
        message=f"Saved {saved} records from '{payload.title}'"
    )
