"""
ScripOx — Deduplication Pipeline
Detects and marks duplicate records within a batch.

Strategy (score-based):
    1. Exact phone match            → definite duplicate
    2. Exact email match            → definite duplicate
    3. Name similarity ≥ 90% +
       same city                   → probable duplicate
    4. Same website domain          → probable duplicate

Returns:
    unique:     list of clean records
    duplicates: list of (record, reason) tuples
"""

from __future__ import annotations
from typing import Any
import re
from difflib import SequenceMatcher
from urllib.parse import urlparse
from loguru import logger


def run_deduplication(
    records: list[dict[str, Any]],
    similarity_threshold: float = 0.90,
) -> tuple[list[dict], list[tuple[dict, str]]]:
    """
    Returns (unique_records, duplicates_with_reason).
    """
    unique:     list[dict]              = []
    duplicates: list[tuple[dict, str]] = []

    seen_phones:  dict[str, int] = {}
    seen_emails:  dict[str, int] = {}
    seen_domains: dict[str, int] = {}

    for idx, rec in enumerate(records):
        reason = _is_duplicate(rec, seen_phones, seen_emails, seen_domains, unique, similarity_threshold)

        if reason:
            rec["_duplicate_reason"] = reason
            duplicates.append((rec, reason))
        else:
            # Register this record's identifiers
            if rec.get("phone"):
                seen_phones[rec["phone"]] = idx
            if rec.get("email"):
                seen_emails[rec["email"].lower()] = idx
            domain = _extract_domain(rec.get("website"))
            if domain:
                seen_domains[domain] = idx
            unique.append(rec)

    logger.info(
        f"Deduplication: {len(records)} total → "
        f"{len(unique)} unique, {len(duplicates)} duplicates"
    )
    return unique, duplicates


def _is_duplicate(
    rec: dict,
    seen_phones: dict,
    seen_emails: dict,
    seen_domains: dict,
    existing: list[dict],
    threshold: float,
) -> str | None:
    # ── Exact phone ──────────────────────────────────────────
    phone = rec.get("phone")
    if phone and phone in seen_phones:
        return f"Duplicate phone: {phone}"

    # ── Exact email ──────────────────────────────────────────
    email = (rec.get("email") or "").lower()
    if email and email in seen_emails:
        return f"Duplicate email: {email}"

    # ── Website domain ───────────────────────────────────────
    domain = _extract_domain(rec.get("website"))
    if domain and domain in seen_domains:
        return f"Duplicate domain: {domain}"

    # ── Fuzzy name + city ────────────────────────────────────
    name = (rec.get("name") or "").lower()
    city = (rec.get("city") or "").lower()
    if name:
        for existing_rec in existing:
            ex_name = (existing_rec.get("name") or "").lower()
            ex_city = (existing_rec.get("city") or "").lower()
            sim = SequenceMatcher(None, name, ex_name).ratio()
            if sim >= threshold and city == ex_city:
                return f"Similar name ({sim:.0%}): '{rec.get('name')}'"

    return None


def _extract_domain(url: str | None) -> str | None:
    if not url:
        return None
    try:
        parsed = urlparse(url if "://" in url else f"https://{url}")
        domain = parsed.netloc.lower().lstrip("www.")
        return domain or None
    except Exception:
        return None
