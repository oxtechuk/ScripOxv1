"""
ScripOx — Application Settings
Reads from .env file or environment variables.
"""

from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    # Server
    HOST: str = "127.0.0.1"
    PORT: int = 8765
    DEBUG: bool = False

    # MySQL
    DB_HOST: str = "localhost"
    DB_PORT: int = 3306
    DB_USER: str = "root"
    DB_PASSWORD: str = ""
    DB_NAME: str = "scripox_db"

    @property
    def DATABASE_URL(self) -> str:
        import os
        # Priority 1: Check LocalAppData\ScripOx\scripox.db (Production Desktop App location)
        local_app_data = os.environ.get("LOCALAPPDATA")
        if local_app_data:
            prod_db = os.path.join(local_app_data, "ScripOx", "scripox.db")
            if os.path.exists(prod_db):
                return f"sqlite+aiosqlite:///{prod_db}"
            
        # Priority 2: Check current directory / app directory
        app_db = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "scripox.db"))
        if os.path.exists(app_db):
            return f"sqlite+aiosqlite:///{app_db}"

        # Priority 3: Fallback dev path
        dev_db = os.path.abspath(os.path.join(
            os.path.dirname(__file__), 
            "..", "..", "ScripOx.Desktop", "bin", "Debug", "net8.0-windows", "win-x64", "scripox.db"
        ))
        if os.path.exists(dev_db):
            return f"sqlite+aiosqlite:///{dev_db}"

        # Default to %LOCALAPPDATA%\ScripOx\scripox.db
        if local_app_data:
            target_dir = os.path.join(local_app_data, "ScripOx")
            os.makedirs(target_dir, exist_ok=True)
            return f"sqlite+aiosqlite:///{os.path.join(target_dir, 'scripox.db')}"

        return f"sqlite+aiosqlite:///{app_db}"

    # JWT
    SECRET_KEY: str = "CHANGE_ME_IN_PRODUCTION_32CHARS_MIN"
    ACCESS_TOKEN_EXPIRE_MINUTES: int = 480  # 8 hours

    # Geocoding
    GEOCODING_PROVIDER: str = "nominatim"  # nominatim | google
    GOOGLE_MAPS_API_KEY: str = ""
    NOMINATIM_USER_AGENT: str = "scripox_oxtech"

    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"


settings = Settings()
