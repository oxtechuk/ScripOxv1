"""
ScripOx — Pydantic DTOs / Schemas
All request/response models for the extractor API.
"""

from __future__ import annotations
from pydantic import BaseModel, Field, HttpUrl, field_validator
from typing import Any, Optional
from enum import Enum
from datetime import datetime


# ── Enums ───────────────────────────────────────────────────

class JobStatus(str, Enum):
    pending   = "pending"
    running   = "running"
    completed = "completed"
    failed    = "failed"
    cancelled = "cancelled"


class RawRecordStatus(str, Enum):
    pending   = "pending"
    approved  = "approved"
    rejected  = "rejected"
    duplicate = "duplicate"


class SourceType(str, Enum):
    web    = "web"
    api    = "api"
    csv    = "csv"
    manual = "manual"


# ── Job DTOs ────────────────────────────────────────────────

class JobCreateRequest(BaseModel):
    source_id:    int
    triggered_by: int
    params:       dict[str, Any] = Field(default_factory=dict)

    model_config = {"json_schema_extra": {
        "example": {
            "source_id": 1,
            "triggered_by": 1,
            "params": {"keyword": "restaurant", "location": "London", "limit": 100}
        }
    }}


class JobStatusResponse(BaseModel):
    id:           int
    source_id:    int
    status:       JobStatus
    total_found:  int
    total_saved:  int
    total_dupes:  int
    total_errors: int
    progress_pct: int
    started_at:   Optional[datetime]
    ended_at:     Optional[datetime]
    error_msg:    Optional[str]
    created_at:   datetime


# ── Raw Record DTOs ─────────────────────────────────────────

class RawRecordDTO(BaseModel):
    job_id:   int
    raw_json: dict[str, Any]
    status:   RawRecordStatus = RawRecordStatus.pending


# ── Company DTOs ────────────────────────────────────────────

class CompanyDTO(BaseModel):
    name:         str
    name_ar:      Optional[str]        = None
    category:     Optional[str]        = None
    phone:        Optional[str]        = None
    phone2:       Optional[str]        = None
    email:        Optional[str]        = None
    website:      Optional[str]        = None
    address:      Optional[str]        = None
    city:         Optional[str]        = None
    region:       Optional[str]        = None
    country:      str                  = "UK"
    postal_code:  Optional[str]        = None
    x:            Optional[float]      = None   # longitude
    y:            Optional[float]      = None   # latitude
    source_name:  Optional[str]        = None
    source_url:   Optional[str]        = None
    raw_record_id: Optional[int]       = None
    links:        list[CompanyLinkDTO] = Field(default_factory=list)

    @field_validator("x")
    @classmethod
    def validate_longitude(cls, v):
        if v is not None and not (-180 <= v <= 180):
            raise ValueError(f"Longitude {v} out of range [-180, 180]")
        return v

    @field_validator("y")
    @classmethod
    def validate_latitude(cls, v):
        if v is not None and not (-90 <= v <= 90):
            raise ValueError(f"Latitude {v} out of range [-90, 90]")
        return v


class CompanyLinkDTO(BaseModel):
    platform: str
    url:      str


# ── Health ──────────────────────────────────────────────────

class HealthResponse(BaseModel):
    status:  str
    version: str
    db:      str
