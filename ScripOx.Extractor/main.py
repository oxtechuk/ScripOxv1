"""
ScripOx Extractor Service — main.py
FastAPI application entry point
Ox Tech — oxtech.uk
"""

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from contextlib import asynccontextmanager
from loguru import logger
import uvicorn
import sys

from api.jobs import router as jobs_router
from api.health import router as health_router
from api.browser_extract import router as browser_router
from db.database import init_db
from core.config import settings


# ── Logging setup ───────────────────────────────────────────
logger.remove()
logger.add(
    sys.stdout,
    format="<green>{time:YYYY-MM-DD HH:mm:ss}</green> | <level>{level}</level> | {message}",
    level="INFO",
)
logger.add(
    "logs/extractor_{time:YYYY-MM-DD}.log",
    rotation="00:00",
    retention="30 days",
    level="DEBUG",
)


# ── Lifespan ────────────────────────────────────────────────
@asynccontextmanager
async def lifespan(app: FastAPI):
    logger.info("ScripOx Extractor starting up...")
    await init_db()
    yield
    logger.info("ScripOx Extractor shut down.")


# ── App ─────────────────────────────────────────────────────
app = FastAPI(
    title="ScripOx Extractor API",
    description="Data extraction and processing service for ScripOx — Ox Tech",
    version="1.0.0",
    docs_url="/docs",
    redoc_url="/redoc",
    lifespan=lifespan,
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],   # Restrict in production to C# app origin
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(health_router,   prefix="/api", tags=["Health"])
app.include_router(jobs_router,     prefix="/api", tags=["Jobs"])
app.include_router(browser_router,  prefix="/api", tags=["Browser Extractor"])


if __name__ == "__main__":
    uvicorn.run(
        "main:app",
        host=settings.HOST,
        port=settings.PORT,
        reload=settings.DEBUG,
    )
