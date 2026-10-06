"""
ScripOx — Base Connector (Abstract)
All source connectors must inherit from this class.
"""

from __future__ import annotations
from abc import ABC, abstractmethod
from typing import Any
from loguru import logger


class BaseConnector(ABC):
    """
    Abstract base for all data-source connectors.

    Lifecycle:
        connector = MyConnector(params)
        records   = await connector.fetch()
        stats     = connector.get_stats()
    """

    def __init__(self, params: dict[str, Any]):
        self.params    = params
        self._fetched  = 0
        self._errors   = 0
        self._log      = logger.bind(connector=self.get_name())

    # ── Abstract interface ───────────────────────────────────

    @abstractmethod
    def get_name(self) -> str:
        """Human-readable connector identifier, e.g. 'google_maps'."""
        ...

    @abstractmethod
    async def fetch(self) -> list[dict[str, Any]]:
        """
        Fetch raw records from the source.
        Returns a list of raw dicts (before normalisation).
        """
        ...

    # ── Shared helpers ───────────────────────────────────────

    def get_stats(self) -> dict[str, int]:
        return {"fetched": self._fetched, "errors": self._errors}

    def _log_progress(self, current: int, total: int):
        pct = int(current / total * 100) if total else 0
        self._log.info(f"Progress: {current}/{total} ({pct}%)")

    @staticmethod
    def _safe_str(value: Any, max_len: int = 500) -> str | None:
        if value is None:
            return None
        return str(value).strip()[:max_len] or None
