"""
ScripOx — Coordinate Enricher
Converts address strings to lat/lng using Nominatim (free)
or Google Geocoding API (if configured).

Falls back gracefully: if geocoding fails, x/y remain None.
"""

from __future__ import annotations
from typing import Optional
import httpx
from loguru import logger
from core.config import settings


# ── Nominatim (OpenStreetMap, free) ─────────────────────────

async def geocode_nominatim(address: str) -> tuple[float, float] | None:
    """Returns (longitude, latitude) or None."""
    if not address:
        return None

    params = {
        "q": address,
        "format": "json",
        "limit": 1,
        "addressdetails": 0,
    }
    headers = {"User-Agent": settings.NOMINATIM_USER_AGENT}

    try:
        async with httpx.AsyncClient(timeout=10) as client:
            resp = await client.get(
                "https://nominatim.openstreetmap.org/search",
                params=params,
                headers=headers,
            )
            resp.raise_for_status()
            results = resp.json()
            if results:
                lat = float(results[0]["lat"])
                lon = float(results[0]["lon"])
                return (lon, lat)
    except Exception as exc:
        logger.warning(f"Nominatim geocoding failed for '{address}': {exc}")
    return None


# ── Google Geocoding API ─────────────────────────────────────

async def geocode_google(address: str, api_key: str) -> tuple[float, float] | None:
    """Returns (longitude, latitude) or None."""
    if not address or not api_key:
        return None

    params = {"address": address, "key": api_key}

    try:
        async with httpx.AsyncClient(timeout=10) as client:
            resp = await client.get(
                "https://maps.googleapis.com/maps/api/geocode/json",
                params=params,
            )
            resp.raise_for_status()
            data = resp.json()
            if data.get("status") == "OK":
                loc = data["results"][0]["geometry"]["location"]
                return (loc["lng"], loc["lat"])
    except Exception as exc:
        logger.warning(f"Google geocoding failed for '{address}': {exc}")
    return None


# ── Unified entry point ──────────────────────────────────────

async def enrich_coordinates(
    record: dict,
    provider: str | None = None,
) -> dict:
    """
    Adds x (longitude) and y (latitude) to the record dict
    if they are missing, using the address field.

    Modifies the dict in place and returns it.
    """
    # Already has coordinates?
    if record.get("x") and record.get("y"):
        return record

    address = record.get("address") or ""
    if not address:
        return record

    provider = provider or settings.GEOCODING_PROVIDER
    coords: tuple[float, float] | None = None

    if provider == "google" and settings.GOOGLE_MAPS_API_KEY:
        coords = await geocode_google(address, settings.GOOGLE_MAPS_API_KEY)

    if coords is None:
        coords = await geocode_nominatim(address)

    if coords:
        record["x"] = coords[0]
        record["y"] = coords[1]
        logger.debug(f"Geocoded '{address}' → {coords}")
    else:
        logger.debug(f"No coordinates found for '{address}'")

    return record
