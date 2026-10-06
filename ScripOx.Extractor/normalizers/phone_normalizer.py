"""
ScripOx — Phone Normalizer
Normalises phone numbers to E.164 international format.
Defaults to UK (+44) if no country code present.
"""

from __future__ import annotations
import re
import phonenumbers
from loguru import logger


DEFAULT_REGION = "GB"  # UK default


def normalize_phone(raw: str | None, region: str = DEFAULT_REGION) -> str | None:
    """
    Returns E.164 formatted phone number or None if invalid.

    Examples:
        "020 7946 0958"   → "+442079460958"
        "+44 20 7946 0958"→ "+442079460958"
        "07911 123456"    → "+447911123456"
        "invalid"         → None
    """
    if not raw:
        return None

    cleaned = re.sub(r"[^\d+]", "", raw.strip())

    try:
        parsed = phonenumbers.parse(cleaned, region)
        if phonenumbers.is_valid_number(parsed):
            return phonenumbers.format_number(parsed, phonenumbers.PhoneNumberFormat.E164)
    except phonenumbers.NumberParseException:
        pass

    logger.debug(f"Could not normalize phone: '{raw}'")
    return None


def normalize_phone_display(raw: str | None, region: str = DEFAULT_REGION) -> str | None:
    """
    Returns national format for display purposes.
    Example: "07911 123456" → "07911 123456"
    """
    e164 = normalize_phone(raw, region)
    if not e164:
        return None
    try:
        parsed = phonenumbers.parse(e164, None)
        return phonenumbers.format_number(parsed, phonenumbers.PhoneNumberFormat.NATIONAL)
    except Exception:
        return e164
