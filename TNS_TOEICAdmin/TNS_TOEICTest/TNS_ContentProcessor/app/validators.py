"""
Input validation logic
"""
import re
from typing import Tuple, List
from langdetect import detect, LangDetectException
from .config import LIMITS, REQUIRED_LANGUAGE
from .models import ValidationError


def validate_text_content(text: str) -> Tuple[bool, List[ValidationError], List[str]]:
    """
    Validate text content
    Returns: (is_valid, errors, warnings)
    """
    errors = []
    warnings = []
    limits = LIMITS["text"]
    
    # Check empty
    if not text or not text.strip():
        errors.append(ValidationError(
            field="content",
            message="Nội dung không được để trống",
            suggestion="Vui lòng nhập văn bản tiếng Anh"
        ))
        return False, errors, warnings
    
    text = text.strip()
    char_count = len(text)
    
    # Check minimum length
    if char_count < limits["min_chars"]:
        errors.append(ValidationError(
            field="content",
            message=f"Nội dung quá ngắn ({char_count} ký tự)",
            suggestion=f"Cần ít nhất {limits['min_chars']} ký tự"
        ))
        return False, errors, warnings
    
    # Check maximum length (warning, not error - user can edit)
    if char_count > limits["max_chars"]:
        over = char_count - limits["max_chars"]
        warnings.append(
            f"Nội dung vượt quá {over} ký tự. Vui lòng cắt bớt để đảm bảo chất lượng phân tích."
        )
    
    # Check language
    try:
        detected_lang = detect(text)
        if detected_lang != REQUIRED_LANGUAGE:
            errors.append(ValidationError(
                field="language",
                message=f"Phát hiện ngôn ngữ: {detected_lang.upper()}",
                suggestion="Hệ thống chỉ hỗ trợ nội dung tiếng Anh. Vui lòng nhập văn bản tiếng Anh."
            ))
            return False, errors, warnings
    except LangDetectException:
        warnings.append("Không thể xác định ngôn ngữ. Đảm bảo nội dung là tiếng Anh.")
    
    return len(errors) == 0, errors, warnings


def validate_file_size(size_bytes: int, max_mb: float) -> Tuple[bool, str]:
    """Check file size"""
    max_bytes = max_mb * 1024 * 1024
    if size_bytes > max_bytes:
        return False, f"File vượt quá {max_mb}MB (hiện tại: {size_bytes / 1024 / 1024:.1f}MB)"
    return True, ""


def validate_file_extension(filename: str, allowed: List[str]) -> Tuple[bool, str]:
    """Check file extension"""
    ext = "." + filename.split(".")[-1].lower() if "." in filename else ""
    if ext not in allowed:
        return False, f"Định dạng không hỗ trợ. Cho phép: {', '.join(allowed)}"
    return True, ""


def validate_duration(duration_seconds: float, max_seconds: float) -> Tuple[bool, str]:
    """Check audio/video duration"""
    if duration_seconds > max_seconds:
        return False, f"Thời lượng vượt quá {max_seconds // 60:.0f} phút (hiện tại: {duration_seconds / 60:.1f} phút)"
    return True, ""


def validate_youtube_url(url: str) -> Tuple[bool, str]:
    """Validate YouTube URL format"""
    youtube_patterns = [
        r"^(https?://)?(www\.)?youtube\.com/watch\?v=[\w-]+",
        r"^(https?://)?(www\.)?youtu\.be/[\w-]+",
        r"^(https?://)?(www\.)?youtube\.com/embed/[\w-]+",
    ]
    
    for pattern in youtube_patterns:
        if re.match(pattern, url):
            return True, ""
    
    return False, "URL YouTube không hợp lệ"