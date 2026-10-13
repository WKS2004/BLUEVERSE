"""Pytest configuration and sys.path setup for marine-conditions tests."""

import sys
from pathlib import Path

# Add services/ai/marine-conditions directory to sys.path so modules can be imported directly
SERVICE_ROOT = Path(__file__).resolve().parents[1]
if str(SERVICE_ROOT) not in sys.path:
    sys.path.insert(0, str(SERVICE_ROOT))
