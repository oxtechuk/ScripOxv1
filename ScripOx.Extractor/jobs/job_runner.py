"""
ScripOx — Job Runner
Orchestrates the full extraction pipeline for a single job.

Flow:
    1. Update job status → running
    2. Resolve connector by source type
    3. Fetch raw records
    4. Save raw records to DB
    5. Run cleaning pipeline
    6. Run deduplication
    7. Save unique companies to DB (with POINT geometry)
    8. Mark duplicates as 'duplicate' in raw_records
    9. Update job status → completed / failed
"""

from __future__ import annotations
from typing import Any
import json
from loguru import logger
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy import text

from connectors.google_maps_connector import GoogleMapsConnector
from connectors.custom_csv_connector import CsvConnector
from connectors.base_connector import BaseConnector
from pipelines.cleaning_pipeline import run_cleaning_pipeline
from pipelines.deduplication_pipeline import run_deduplication
from db.database import AsyncSessionLocal


# ── Connector registry ───────────────────────────────────────
CONNECTOR_MAP: dict[str, type[BaseConnector]] = {
    "google_maps": GoogleMapsConnector,
    "csv_file":    CsvConnector,
}

# Fallback map when connector name can't be read from DB
SOURCE_ID_TO_CONNECTOR: dict[int, str] = {
    1: "google_maps",
    2: "csv_file",
}


async def run_job(job_id: int, db: AsyncSession | None = None) -> None:
    """Main entry point — runs a full extraction job.

    If `db` is not provided, a new session is created internally.
    """
    if db is None:
        async with AsyncSessionLocal() as session:
            await _do_run_job(job_id, session)
    else:
        await _do_run_job(job_id, db)


async def _do_run_job(job_id: int, db: AsyncSession) -> None:
    """Internal implementation — runs the full extraction pipeline."""
    try:
        # ── 1. Load job ──────────────────────────────────────
        row = await db.execute(
            text("SELECT * FROM extraction_jobs WHERE id = :id"),
            {"id": job_id}
        )
        job = row.mappings().first()
        if not job:
            raise ValueError(f"Job {job_id} not found")

        params: dict[str, Any] = job["params_json"] or {}
        if isinstance(params, str):
            try:
                params = json.loads(params)
            except json.JSONDecodeError:
                # params were stored via str(dict) — convert single quotes
                params = json.loads(params.replace("'", '"'))

        # ── 2. Load source ───────────────────────────────────
        src_row = await db.execute(
            text("SELECT * FROM sources WHERE id = :id"),
            {"id": job["source_id"]}
        )
        source = src_row.mappings().first()

        # Prefer connector name from sources table; fall back to source_id map
        if source and source.get("connector"):
            connector_name = source["connector"]
        else:
            connector_name = SOURCE_ID_TO_CONNECTOR.get(job["source_id"], "csv_file")
            logger.warning(
                f"Job {job_id}: source {job['source_id']} not found in DB, "
                f"defaulting connector to '{connector_name}'"
            )

        # Guard: csv_file requires a file_path param
        if connector_name == "csv_file" and not params.get("file_path"):
            raise ValueError(
                "connector 'csv_file' requires a 'file_path' parameter. "
                "Please supply it when creating the job."
            )

        # ── 3. Update job → running ──────────────────────────
        await db.execute(
            text("""
                UPDATE extraction_jobs
                SET status='running', started_at=CURRENT_TIMESTAMP, progress_pct=5
                WHERE id=:id
            """),
            {"id": job_id}
        )
        await db.commit()

        # ── 4. Run connector ─────────────────────────────────
        cls       = CONNECTOR_MAP.get(connector_name, GoogleMapsConnector)
        connector = cls(params)
        raw_records = await connector.fetch()

        logger.info(f"Job {job_id}: fetched {len(raw_records)} raw records via '{connector_name}'")

        # ── 5. Save raw records ──────────────────────────────
        raw_ids: list[int] = []
        for rec in raw_records:
            result = await db.execute(
                text("""
                    INSERT INTO raw_records (job_id, raw_json, status)
                    VALUES (:job_id, :raw_json, 'pending')
                """),
                {"job_id": job_id, "raw_json": json.dumps(rec, ensure_ascii=False)}
            )
            raw_ids.append(result.lastrowid)
        await db.commit()

        # ── 6. Update progress 40% ───────────────────────────
        await db.execute(
            text("UPDATE extraction_jobs SET progress_pct=40, total_found=:n WHERE id=:id"),
            {"n": len(raw_records), "id": job_id}
        )
        await db.commit()

        # ── 7. Cleaning pipeline ─────────────────────────────
        cleaned = await run_cleaning_pipeline(raw_records, geocode=True)

        # ── 8. Deduplication ─────────────────────────────────
        unique, duplicates = run_deduplication(cleaned)

        # ── 9. Save companies ────────────────────────────────
        saved = 0
        for rec in unique:
            try:
                x = rec.get("x")
                y = rec.get("y")
                # Use 0,0 if no coordinates (POINT column is NOT NULL)
                point_sql = f"ST_GeomFromText('POINT({x or 0} {y or 0})', 4326)"
                await db.execute(
                    text(f"""
                        INSERT INTO companies
                          (name, category, phone, phone2, email, website,
                           address, city, country, postal_code,
                           x, y, location,
                           source_id, source_name, source_url,
                           status_id, priority, created_at)
                        VALUES
                          (:name, :category, :phone, :phone2, :email, :website,
                           :address, :city, :country, :postal_code,
                           :x, :y, {point_sql},
                           :source_id, :source_name, :source_url,
                           1, 'medium', CURRENT_TIMESTAMP)
                    """),
                    {
                        "name":        rec.get("name"),
                        "category":    rec.get("category"),
                        "phone":       rec.get("phone"),
                        "phone2":      rec.get("phone2"),
                        "email":       rec.get("email"),
                        "website":     rec.get("website"),
                        "address":     rec.get("address"),
                        "city":        rec.get("city"),
                        "country":     rec.get("country", "UK"),
                        "postal_code": rec.get("postal_code"),
                        "x":           x,
                        "y":           y,
                        "source_id":   job["source_id"],
                        "source_name": rec.get("source_name"),
                        "source_url":  rec.get("source_url"),
                    }
                )
                saved += 1
            except Exception as exc:
                logger.error(f"Failed to save company '{rec.get('name')}': {exc}")

        await db.commit()

        # ── 10. Complete ─────────────────────────────────────
        await db.execute(
            text("""
                UPDATE extraction_jobs
                SET status='completed', ended_at=CURRENT_TIMESTAMP, progress_pct=100,
                    total_saved=:saved, total_dupes=:dupes
                WHERE id=:id
            """),
            {"saved": saved, "dupes": len(duplicates), "id": job_id}
        )
        await db.commit()

        logger.success(
            f"Job {job_id} completed: {len(raw_records)} fetched, "
            f"{saved} saved, {len(duplicates)} duplicates"
        )

    except Exception as exc:
        logger.error(f"Job {job_id} failed: {exc}")
        try:
            await db.execute(
                text("""
                    UPDATE extraction_jobs
                    SET status='failed', ended_at=CURRENT_TIMESTAMP, error_msg=:err
                    WHERE id=:id
                """),
                {"err": str(exc)[:500], "id": job_id}
            )
            await db.commit()
        except Exception as db_exc:
            logger.error(f"Could not update failed status for job {job_id}: {db_exc}")
        raise