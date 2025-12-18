"""
Configuration settings for Content Processor
"""
from pathlib import Path

# Service URLs
WHISPER_SERVICE_URL = "http://localhost:5003"

# Directories
BASE_DIR = Path(__file__).parent.parent
TEMP_DIR = BASE_DIR / "temp"
TEMP_DIR.mkdir(exist_ok=True)

# Input Limits
LIMITS = {
    "text": {
        "max_chars": 1500,          # ~300 từ
        "min_chars": 50,            # Tối thiểu
    },
    "pdf": {
        "max_size_mb": 2,
        "max_pages": 2,
        "max_chars": 3000,          # ~600 từ
    },
    "docx": {
        "max_size_mb": 2,
        "max_pages": 2,
        "max_chars": 3000,          # ~600 từ
    },
    "audio": {
        "max_size_mb": 10,
        "max_duration_seconds": 120,  # 2 phút
        "allowed_formats": [".mp3", ".wav", ".m4a", ".webm", ".ogg", ".flac"],
    },
    "video": {
        "max_size_mb": 30,
        "max_duration_seconds": 120,  # 2 phút
        "allowed_formats": [".mp4", ".mov", ".avi", ".mkv", ".webm"],
    },
    "youtube": {
        "max_duration_seconds": 180,  # 3 phút
    },
}

# Supported languages (chỉ chấp nhận tiếng Anh)
REQUIRED_LANGUAGE = "en"