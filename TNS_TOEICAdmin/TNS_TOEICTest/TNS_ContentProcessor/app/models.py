"""
Pydantic models for request/response
"""
from pydantic import BaseModel, Field
from typing import Optional, List
from enum import Enum


class InputType(str, Enum):
    TEXT = "text"
    PDF = "pdf"
    DOCX = "docx"
    AUDIO = "audio"
    VIDEO = "video"
    YOUTUBE = "youtube"


class ValidationError(BaseModel):
    field: str
    message: str
    suggestion: Optional[str] = None


class PreviewSegment(BaseModel):
    """Segment với timestamp (cho audio/video)"""
    id: int
    start_time: float
    end_time: float
    text: str


class ProcessResult(BaseModel):
    """Kết quả sau khi xử lý input"""
    success: bool
    input_type: InputType
    
    # Content
    extracted_text: Optional[str] = None
    word_count: int = 0
    char_count: int = 0
    
    # Metadata
    language_detected: Optional[str] = None
    is_english: bool = False
    duration_seconds: Optional[float] = None  # Cho audio/video
    page_count: Optional[int] = None          # Cho PDF/DOCX
    
    # Segments (cho audio/video với timestamps)
    segments: Optional[List[PreviewSegment]] = None
    
    # Validation
    is_valid: bool = False
    errors: List[ValidationError] = []
    warnings: List[str] = []
    
    # Limits info
    max_chars_allowed: int = 0
    chars_over_limit: int = 0  # Số ký tự vượt quá (nếu có)


class TextInput(BaseModel):
    """Input dạng text trực tiếp"""
    content: str = Field(..., min_length=1)


class YouTubeInput(BaseModel):
    """Input dạng YouTube URL"""
    url: str = Field(..., pattern=r"^(https?://)?(www\.)?(youtube\.com|youtu\.be)/.+$")