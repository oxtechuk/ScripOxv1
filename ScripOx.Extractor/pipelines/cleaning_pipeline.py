"""
ScripOx — Cleaning Pipeline
Cleans and normalises a list of raw record dicts.

Steps:
    1. Strip whitespace from all string fields
    2. Normalize phone numbers (E.164)
    3. Lowercase email
    4. Ensure website has https://
    5. Enrich coordinates from address (if missing)
    6. Truncate oversized fields
"""

from __future__ import annotations
import re
from typing import Any
from loguru import logger

from normalizers.phone_normalizer import normalize_phone
from normalizers.coordinate_enricher import enrich_coordinates


_MAX_LENGTHS = {
    "name":        500,
    "category":    200,
    "phone":       50,
    "email":       255,
    "website":     500,
    "address":     1000,
    "city":        200,
    "region":      200,
    "country":     100,
    "postal_code": 20,
    "source_url":  500,
}


async def run_cleaning_pipeline(
    records: list[dict[str, Any]],
    geocode: bool = True,
) -> list[dict[str, Any]]:
    """
    Cleans a list of raw dicts.
    Returns the cleaned list (some records may be dropped if name is missing).
    """
    cleaned = []

    for raw in records:
        try:
            rec = await _clean_record(raw, geocode=geocode)
            if rec:
                cleaned.append(rec)
        except Exception as exc:
            logger.warning(f"Cleaning failed for record: {exc}")

    logger.info(f"Cleaning pipeline: {len(records)} in → {len(cleaned)} out")
    return cleaned


async def _clean_record(raw: dict[str, Any], geocode: bool) -> dict[str, Any] | None:
    rec: dict[str, Any] = {}

    # ── 1. Strip strings ─────────────────────────────────────
    for key, val in raw.items():
        if isinstance(val, str):
            rec[key] = val.strip() or None
        else:
            rec[key] = val

    # ── 2. Name is mandatory ─────────────────────────────────
    name = rec.get("name")
    if not name:
        return None

    # ── 3. Phone normalization ───────────────────────────────
    for phone_field in ("phone", "phone2"):
        raw_phone = rec.get(phone_field)
        if raw_phone:
            rec[phone_field] = normalize_phone(raw_phone)

    # ── 4. Email lowercase ───────────────────────────────────
    if rec.get("email"):
        rec["email"] = rec["email"].lower().strip()

    # ── 5. Website → ensure https:// ─────────────────────────
    website = rec.get("website")
    if website and not re.match(r"^https?://", website, re.I):
        rec["website"] = f"https://{website}"

    # ── 6. Geocoding ─────────────────────────────────────────
    if geocode:
        rec = await enrich_coordinates(rec)

    # ── 7. Truncate fields ───────────────────────────────────
    for field, max_len in _MAX_LENGTHS.items():
        if rec.get(field) and len(str(rec[field])) > max_len:
            rec[field] = str(rec[field])[:max_len]

    return rec
