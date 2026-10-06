"""
ScripOx — Google Maps / Places API Connector
Fetches businesses using the Google Places Text Search API.

Required param keys:
    keyword  : search term,          e.g. "restaurant"
    location : city or address,      e.g. "London, UK"
    limit    : max results (≤ 100)
    api_key  : Google Maps API key   (falls back to settings)
"""

from __future__ import annotations
from typing import Any
import httpx
from loguru import logger

from connectors.base_connector import BaseConnector
from core.config import settings


PLACES_URL = "https://maps.googleapis.com/maps/api/place/textsearch/json"
DETAILS_URL = "https://maps.googleapis.com/maps/api/place/details/json"


class GoogleMapsConnector(BaseConnector):

    def get_name(self) -> str:
        return "google_maps"

    async def fetch(self) -> list[dict[str, Any]]:
        api_key  = self.params.get("api_key") or settings.GOOGLE_MAPS_API_KEY
        keyword  = self.params.get("keyword", "")
        location = self.params.get("location", "")
        limit    = min(int(self.params.get("limit", 60)), 120)

        if not api_key:
            raise ValueError("Google Maps API key is required")

        query   = f"{keyword} in {location}".strip()
        results = []
        page_token: str | None = None

        async with httpx.AsyncClient(timeout=30) as client:
            while len(results) < limit:
                params: dict[str, Any] = {
                    "query": query,
                    "key": api_key,
                    "language": "en",
                }
                if page_token:
                    params["pagetoken"] = page_token

                resp = await client.get(PLACES_URL, params=params)
                resp.raise_for_status()
                data = resp.json()

                if data.get("status") not in ("OK", "ZERO_RESULTS"):
                    self._log.error(f"Places API error: {data.get('status')} — {data.get('error_message','')}")
                    break

                for place in data.get("results", []):
                    if len(results) >= limit:
                        break
                    record = self._parse_place(place)
                    results.append(record)
                    self._fetched += 1

                page_token = data.get("next_page_token")
                if not page_token:
                    break

        self._log.info(f"Fetched {len(results)} results for '{query}'")
        return results

    # ── Private helpers ──────────────────────────────────────

    def _parse_place(self, place: dict) -> dict[str, Any]:
        geo    = place.get("geometry", {}).get("location", {})
        return {
            "name":        self._safe_str(place.get("name")),
            "address":     self._safe_str(place.get("formatted_address")),
            "phone":       None,   # Requires Details call
            "website":     None,
            "x":           geo.get("lng"),
            "y":           geo.get("lat"),
            "place_id":    place.get("place_id"),
            "rating":      place.get("rating"),
            "category":    ", ".join(place.get("types", [])),
            "source_url":  f"https://maps.google.com/?cid={place.get('place_id', '')}",
            "_raw":        place,  # Keep original for raw_records
        }
