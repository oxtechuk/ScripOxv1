"""
ScripOx — Jobs API Router
POST /api/jobs    — create and start a new extraction job (background task)
GET  /api/jobs    — list all jobs (paginated)
GET  /api/jobs/{id} — get job status
DELETE /api/jobs/{id} — cancel a running job
"""

from __future__ import annotations
import asyncio
from fastapi import APIRouter, Depends, HTTPException, BackgroundTasks
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy import text

from db.database import get_db, AsyncSessionLocal
from dto.schemas import JobCreateRequest, JobStatusResponse
from jobs.job_runner import run_job

router = APIRouter()


@router.post("/jobs", response_model=JobStatusResponse, status_code=201)
async def create_job(
    payload: JobCreateRequest,
    background_tasks: BackgroundTasks,
    db: AsyncSession = Depends(get_db),
):
    """Trigger a new extraction job. Runs asynchronously in the background."""

    # Verify source exists
    src = await db.execute(
        text("SELECT id FROM sources WHERE id = :id AND is_active = 1"),
        {"id": payload.source_id}
    )
    if not src.first():
        raise HTTPException(status_code=404, detail="Source not found or inactive")

    # Insert job record
    result = await db.execute(
        text("""
            INSERT INTO extraction_jobs
              (source_id, triggered_by, status, params_json)
            VALUES (:source_id, :triggered_by, 'pending', :params)
        """),
        {
            "source_id":    payload.source_id,
            "triggered_by": payload.triggered_by,
            "params":       str(payload.params).replace("'", '"'),
        }
    )
    await db.commit()
    job_id = result.lastrowid

    # Fire off background extraction.
    # IMPORTANT: We pass job_id ONLY — run_job opens its own DB session.
    # The request-scoped `db` session closes after this response returns.
    background_tasks.add_task(_run_job_in_background, job_id)

    # Return initial status
    return await _get_job(job_id, db)


async def _run_job_in_background(job_id: int) -> None:
    """Wrapper that gives run_job its own DB session (request session is closed by now)."""
    async with AsyncSessionLocal() as session:
        await run_job(job_id, session)


@router.get("/jobs", response_model=list[JobStatusResponse])
async def list_jobs(
    page: int = 1,
    page_size: int = 20,
    db: AsyncSession = Depends(get_db),
):
    """Return paginated list of extraction jobs."""
    offset = (page - 1) * page_size
    rows = await db.execute(
        text("""
            SELECT * FROM extraction_jobs
            ORDER BY created_at DESC
            LIMIT :limit OFFSET :offset
        """),
        {"limit": page_size, "offset": offset}
    )
    return [dict(r) for r in rows.mappings()]


@router.get("/jobs/{job_id}", response_model=JobStatusResponse)
async def get_job_status(job_id: int, db: AsyncSession = Depends(get_db)):
    """Return the current status of a specific job."""
    return await _get_job(job_id, db)


@router.delete("/jobs/{job_id}", status_code=204)
async def cancel_job(job_id: int, db: AsyncSession = Depends(get_db)):
    """Mark a pending/running job as cancelled."""
    row = await db.execute(
        text("SELECT status FROM extraction_jobs WHERE id = :id"),
        {"id": job_id}
    )
    job = row.mappings().first()
    if not job:
        raise HTTPException(status_code=404, detail="Job not found")
    if job["status"] not in ("pending", "running"):
        raise HTTPException(status_code=400, detail=f"Cannot cancel a job in '{job['status']}' state")

    await db.execute(
        text("UPDATE extraction_jobs SET status='cancelled', ended_at=CURRENT_TIMESTAMP WHERE id=:id"),
        {"id": job_id}
    )
    await db.commit()


# ── Helper ───────────────────────────────────────────────────

async def _get_job(job_id: int, db: AsyncSession) -> dict:
    row = await db.execute(
        text("SELECT * FROM extraction_jobs WHERE id = :id"),
        {"id": job_id}
    )
    job = row.mappings().first()
    if not job:
        raise HTTPException(status_code=404, detail="Job not found")
    return dict(job)
