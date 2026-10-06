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
        # We need to point to the exact same DB file used by the Desktop app
        db_path = os.path.abspath(os.path.join(
            os.path.dirname(__file__), 
            "..", "..", "ScripOx.Desktop", "bin", "Debug", "net8.0-windows", "win-x64", "scripox.db"
        ))
        # Ensure directory exists in case backend starts before desktop build
        os.makedirs(os.path.dirname(db_path), exist_ok=True)
        return f"sqlite+aiosqlite:///{db_path}"

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
