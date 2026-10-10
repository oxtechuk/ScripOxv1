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
if sys.stdout is not None:
    logger.add(
        sys.stdout,
        format="<green>{time:YYYY-MM-DD HH:mm:ss}</green> | <level>{level}</level> | {message}",
        level="INFO",
    )

import os
local_app_data = os.environ.get("LOCALAPPDATA")
if local_app_data:
    log_dir = os.path.join(local_app_data, "ScripOx", "logs")
else:
    log_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "logs")

try:
    os.makedirs(log_dir, exist_ok=True)
    logger.add(
        os.path.join(log_dir, "extractor_{time:YYYY-MM-DD}.log"),
        rotation="00:00",
        retention="30 days",
        level="DEBUG",
    )
except Exception:
    pass


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
    import argparse
    parser = argparse.ArgumentParser(description="ScripOx Extractor Service")
    parser.add_argument("--port", type=int, default=settings.PORT, help="Port to listen on")
    parser.add_argument("--host", type=str, default=settings.HOST, help="Host to bind to")
    args, _ = parser.parse_known_args()

    # Pass the app object directly to allow frozen PyInstaller executables to run
    uvicorn.run(
        app,
        host=args.host,
        port=args.port,
        reload=False,
    )
