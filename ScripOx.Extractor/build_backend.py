"""
Script to freeze ScripOx.Extractor into a standalone ScripOxBackend.exe using PyInstaller.
This produces a self-contained executable that bundles Python and all dependencies,
requiring NO Python installation on the client's PC.
"""
import os
import sys
import subprocess
import shutil

ROOT_DIR = os.path.dirname(os.path.abspath(__file__))
MAIN_PY = os.path.join(ROOT_DIR, "main.py")
OUTPUT_DIR = os.path.join(ROOT_DIR, "dist")
EXE_NAME = "ScripOxBackend.exe"

HIDDEN_IMPORTS = [
    "uvicorn",
    "uvicorn.logging",
    "uvicorn.loops",
    "uvicorn.loops.auto",
    "uvicorn.loops.asyncio",
    "uvicorn.protocols",
    "uvicorn.protocols.http",
    "uvicorn.protocols.http.auto",
    "uvicorn.protocols.http.h11_impl",
    "uvicorn.protocols.websockets",
    "uvicorn.protocols.websockets.auto",
    "uvicorn.lifespans",
    "uvicorn.lifespans.on",
    "aiosqlite",
    "sqlite3",
    "sqlalchemy",
    "sqlalchemy.dialects.sqlite",
    "sqlalchemy.dialects.sqlite.aiosqlite",
    "sqlalchemy.ext.asyncio",
    "greenlet",
    "greenlet._greenlet",
    "fastapi",
    "pydantic",
    "pydantic_settings",
    "loguru",
]

def build():
    print("=" * 60)
    print("Building Standalone ScripOxBackend.exe with PyInstaller...")
    print("=" * 60)

    icon_path = os.path.join(ROOT_DIR, "..", "ScripOx.Desktop", "Resources", "Icons", "app.ico")
    version_file = os.path.join(ROOT_DIR, "file_version_info.txt")

    cmd = [
        sys.executable,
        "-m", "PyInstaller",
        "--name", "ScripOxBackend",
        "--onedir",
        "--clean",
        "--noconsole",
        "--collect-all", "greenlet",
        "--distpath", OUTPUT_DIR,
        "--workpath", os.path.join(ROOT_DIR, "build"),
    ]

    if os.path.exists(icon_path):
        cmd.extend(["--icon", icon_path])
    if os.path.exists(version_file):
        cmd.extend(["--version-file", version_file])

    for imp in HIDDEN_IMPORTS:
        cmd.extend(["--hidden-import", imp])

    cmd.append(MAIN_PY)

    print("Running command:", " ".join(cmd))
    res = subprocess.run(cmd, cwd=ROOT_DIR)
    if res.returncode != 0:
        print("PyInstaller build failed with exit code:", res.returncode)
        sys.exit(res.returncode)

    built_exe = os.path.join(OUTPUT_DIR, "ScripOxBackend", EXE_NAME)
    if os.path.exists(built_exe):
        size_mb = os.path.getsize(built_exe) / (1024 * 1024)
        print(f"SUCCESS: Built {built_exe} ({size_mb:.2f} MB)")
    else:
        print("ERROR: Output executable not found at:", built_exe)
        sys.exit(1)

if __name__ == "__main__":
    build()
