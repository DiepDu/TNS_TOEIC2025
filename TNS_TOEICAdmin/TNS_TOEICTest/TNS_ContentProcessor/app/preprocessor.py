"""
Pre-processor: Extract content from various input types
"""
import os
import tempfile
import uuid
from pathlib import Path
from typing import Optional
import requests
import pdfplumber
from docx import Document
import yt_dlp
from moviepy.editor import VideoFileClip

from .config import LIMITS, TEMP_DIR, WHISPER_SERVICE_URL
from .models import ProcessResult, InputType, ValidationError, PreviewSegment
from .validators import (
    validate_text_content,
    validate_file_size,
    validate_file_extension,
    validate_duration,
    validate_youtube_url,
)
from langdetect import detect, LangDetectException


class PreProcessor:
    """Main pre-processor class"""
    
    def __init__(self):
        self.temp_dir = TEMP_DIR
    
    # ==================== TEXT ====================
    def process_text(self, content: str) -> ProcessResult:
        """Process direct text input"""
        limits = LIMITS["text"]
        
        is_valid, errors, warnings = validate_text_content(content)
        
        # Detect language
        lang = None
        try:
            lang = detect(content)
        except:
            pass
        
        char_count = len(content.strip())
        word_count = len(content.split())
        
        return ProcessResult(
            success=True,
            input_type=InputType.TEXT,
            extracted_text=content.strip(),
            word_count=word_count,
            char_count=char_count,
            language_detected=lang,
            is_english=(lang == "en"),
            is_valid=is_valid,
            errors=errors,
            warnings=warnings,
            max_chars_allowed=limits["max_chars"],
            chars_over_limit=max(0, char_count - limits["max_chars"]),
        )
    
    # ==================== PDF ====================
    def process_pdf(self, file_path: str, file_size: int) -> ProcessResult:
        """Extract text from PDF"""
        limits = LIMITS["pdf"]
        errors = []
        warnings = []
        
        # Validate file size
        size_ok, size_msg = validate_file_size(file_size, limits["max_size_mb"])
        if not size_ok:
            errors.append(ValidationError(field="file", message=size_msg))
            return ProcessResult(
                success=False,
                input_type=InputType.PDF,
                is_valid=False,
                errors=errors,
                max_chars_allowed=limits["max_chars"],
            )
        
        try:
            extracted_text = ""
            page_count = 0
            
            with pdfplumber.open(file_path) as pdf:
                page_count = len(pdf.pages)
                
                # Check page limit
                if page_count > limits["max_pages"]:
                    errors.append(ValidationError(
                        field="pages",
                        message=f"PDF có {page_count} trang, vượt quá giới hạn {limits['max_pages']} trang",
                        suggestion=f"Vui lòng sử dụng PDF có tối đa {limits['max_pages']} trang"
                    ))
                    return ProcessResult(
                        success=False,
                        input_type=InputType.PDF,
                        page_count=page_count,
                        is_valid=False,
                        errors=errors,
                        max_chars_allowed=limits["max_chars"],
                    )
                
                # Extract text from each page
                for page in pdf.pages[:limits["max_pages"]]:
                    page_text = page.extract_text() or ""
                    extracted_text += page_text + "\n"
            
            extracted_text = self._clean_text(extracted_text)
            
            # Validate extracted content
            if not extracted_text.strip():
                errors.append(ValidationError(
                    field="content",
                    message="Không thể trích xuất văn bản từ PDF",
                    suggestion="PDF có thể là dạng scan/hình ảnh. Vui lòng sử dụng PDF có text"
                ))
                return ProcessResult(
                    success=False,
                    input_type=InputType.PDF,
                    is_valid=False,
                    errors=errors,
                    max_chars_allowed=limits["max_chars"],
                )
            
            # Check language
            lang = None
            is_english = False
            try:
                lang = detect(extracted_text)
                is_english = (lang == "en")
                if not is_english:
                    errors.append(ValidationError(
                        field="language",
                        message=f"Phát hiện ngôn ngữ: {lang.upper()}",
                        suggestion="Hệ thống chỉ hỗ trợ nội dung tiếng Anh"
                    ))
            except:
                warnings.append("Không thể xác định ngôn ngữ")
            
            char_count = len(extracted_text)
            word_count = len(extracted_text.split())
            
            # Check content length
            if char_count > limits["max_chars"]:
                over = char_count - limits["max_chars"]
                warnings.append(f"Nội dung vượt quá {over} ký tự. Vui lòng chỉnh sửa bớt.")
            
            return ProcessResult(
                success=True,
                input_type=InputType.PDF,
                extracted_text=extracted_text,
                word_count=word_count,
                char_count=char_count,
                page_count=page_count,
                language_detected=lang,
                is_english=is_english,
                is_valid=(len(errors) == 0 and is_english),
                errors=errors,
                warnings=warnings,
                max_chars_allowed=limits["max_chars"],
                chars_over_limit=max(0, char_count - limits["max_chars"]),
            )
            
        except Exception as e:
            errors.append(ValidationError(
                field="file",
                message=f"Lỗi đọc PDF: {str(e)}"
            ))
            return ProcessResult(
                success=False,
                input_type=InputType.PDF,
                is_valid=False,
                errors=errors,
                max_chars_allowed=limits["max_chars"],
            )
    
    # ==================== DOCX ====================
    def process_docx(self, file_path: str, file_size: int) -> ProcessResult:
        """Extract text from DOCX"""
        limits = LIMITS["docx"]
        errors = []
        warnings = []
        
        # Validate file size
        size_ok, size_msg = validate_file_size(file_size, limits["max_size_mb"])
        if not size_ok:
            errors.append(ValidationError(field="file", message=size_msg))
            return ProcessResult(
                success=False,
                input_type=InputType.DOCX,
                is_valid=False,
                errors=errors,
                max_chars_allowed=limits["max_chars"],
            )
        
        try:
            doc = Document(file_path)
            
            # Extract text from paragraphs
            extracted_text = "\n".join([para.text for para in doc.paragraphs if para.text.strip()])
            extracted_text = self._clean_text(extracted_text)
            
            if not extracted_text.strip():
                errors.append(ValidationError(
                    field="content",
                    message="Không thể trích xuất văn bản từ DOCX",
                    suggestion="File có thể trống hoặc chỉ chứa hình ảnh"
                ))
                return ProcessResult(
                    success=False,
                    input_type=InputType.DOCX,
                    is_valid=False,
                    errors=errors,
                    max_chars_allowed=limits["max_chars"],
                )
            
            # Estimate page count (rough: ~500 words per page)
            word_count = len(extracted_text.split())
            page_count = max(1, word_count // 500 + 1)
            
            if page_count > limits["max_pages"]:
                warnings.append(f"Ước tính {page_count} trang, có thể vượt giới hạn")
            
            # Check language
            lang = None
            is_english = False
            try:
                lang = detect(extracted_text)
                is_english = (lang == "en")
                if not is_english:
                    errors.append(ValidationError(
                        field="language",
                        message=f"Phát hiện ngôn ngữ: {lang.upper()}",
                        suggestion="Hệ thống chỉ hỗ trợ nội dung tiếng Anh"
                    ))
            except:
                warnings.append("Không thể xác định ngôn ngữ")
            
            char_count = len(extracted_text)
            
            if char_count > limits["max_chars"]:
                over = char_count - limits["max_chars"]
                warnings.append(f"Nội dung vượt quá {over} ký tự. Vui lòng chỉnh sửa bớt.")
            
            return ProcessResult(
                success=True,
                input_type=InputType.DOCX,
                extracted_text=extracted_text,
                word_count=word_count,
                char_count=char_count,
                page_count=page_count,
                language_detected=lang,
                is_english=is_english,
                is_valid=(len(errors) == 0 and is_english),
                errors=errors,
                warnings=warnings,
                max_chars_allowed=limits["max_chars"],
                chars_over_limit=max(0, char_count - limits["max_chars"]),
            )
            
        except Exception as e:
            errors.append(ValidationError(field="file", message=f"Lỗi đọc DOCX: {str(e)}"))
            return ProcessResult(
                success=False,
                input_type=InputType.DOCX,
                is_valid=False,
                errors=errors,
                max_chars_allowed=limits["max_chars"],
            )
    
    # ==================== AUDIO ====================
    def process_audio(self, file_path: str, file_size: int, filename: str) -> ProcessResult:
        """Process audio file via Whisper service"""
        limits = LIMITS["audio"]
        errors = []
        warnings = []
        
        # Validate file size
        size_ok, size_msg = validate_file_size(file_size, limits["max_size_mb"])
        if not size_ok:
            errors.append(ValidationError(field="file", message=size_msg))
            return ProcessResult(
                success=False,
                input_type=InputType.AUDIO,
                is_valid=False,
                errors=errors,
                max_chars_allowed=LIMITS["text"]["max_chars"],
            )
        
        # Validate format
        ext_ok, ext_msg = validate_file_extension(filename, limits["allowed_formats"])
        if not ext_ok:
            errors.append(ValidationError(field="format", message=ext_msg))
            return ProcessResult(
                success=False,
                input_type=InputType.AUDIO,
                is_valid=False,
                errors=errors,
                max_chars_allowed=LIMITS["text"]["max_chars"],
            )
        
        # Call Whisper service
        try:
            result = self._call_whisper(file_path)
            if not result:
                errors.append(ValidationError(
                    field="whisper",
                    message="Không thể kết nối Whisper service",
                    suggestion="Đảm bảo Whisper đang chạy tại localhost:5003"
                ))
                return ProcessResult(
                    success=False,
                    input_type=InputType.AUDIO,
                    is_valid=False,
                    errors=errors,
                    max_chars_allowed=LIMITS["text"]["max_chars"],
                )
            
            duration = result.get("duration_seconds", 0)
            
            # Check duration
            dur_ok, dur_msg = validate_duration(duration, limits["max_duration_seconds"])
            if not dur_ok:
                errors.append(ValidationError(field="duration", message=dur_msg))
                return ProcessResult(
                    success=False,
                    input_type=InputType.AUDIO,
                    duration_seconds=duration,
                    is_valid=False,
                    errors=errors,
                    max_chars_allowed=LIMITS["text"]["max_chars"],
                )
            
            extracted_text = result.get("full_text", "").strip()
            
            # Check language
            lang = result.get("language", "en")
            is_english = (lang == "en")
            if not is_english:
                errors.append(ValidationError(
                    field="language",
                    message=f"Phát hiện ngôn ngữ: {lang.upper()}",
                    suggestion="Hệ thống chỉ hỗ trợ nội dung tiếng Anh"
                ))
            
            # Build segments
            segments = []
            for seg in result.get("segments", []):
                segments.append(PreviewSegment(
                    id=seg["id"],
                    start_time=seg["start_time"],
                    end_time=seg["end_time"],
                    text=seg["text"],
                ))
            
            char_count = len(extracted_text)
            word_count = len(extracted_text.split())
            max_chars = LIMITS["text"]["max_chars"]
            
            if char_count > max_chars:
                over = char_count - max_chars
                warnings.append(f"Transcript vượt quá {over} ký tự. Vui lòng chỉnh sửa bớt.")
            
            return ProcessResult(
                success=True,
                input_type=InputType.AUDIO,
                extracted_text=extracted_text,
                word_count=word_count,
                char_count=char_count,
                duration_seconds=duration,
                language_detected=lang,
                is_english=is_english,
                segments=segments,
                is_valid=(len(errors) == 0 and is_english),
                errors=errors,
                warnings=warnings,
                max_chars_allowed=max_chars,
                chars_over_limit=max(0, char_count - max_chars),
            )
            
        except Exception as e:
            errors.append(ValidationError(field="process", message=f"Lỗi xử lý audio: {str(e)}"))
            return ProcessResult(
                success=False,
                input_type=InputType.AUDIO,
                is_valid=False,
                errors=errors,
                max_chars_allowed=LIMITS["text"]["max_chars"],
            )
    
    # ==================== VIDEO ====================
    def process_video(self, file_path: str, file_size: int, filename: str) -> ProcessResult:
        """Process video file: extract audio then call Whisper"""
        limits = LIMITS["video"]
        errors = []
        warnings = []
        
        # Validate file size
        size_ok, size_msg = validate_file_size(file_size, limits["max_size_mb"])
        if not size_ok:
            errors.append(ValidationError(field="file", message=size_msg))
            return ProcessResult(
                success=False,
                input_type=InputType.VIDEO,
                is_valid=False,
                errors=errors,
                max_chars_allowed=LIMITS["text"]["max_chars"],
            )
        
        # Validate format
        ext_ok, ext_msg = validate_file_extension(filename, limits["allowed_formats"])
        if not ext_ok:
            errors.append(ValidationError(field="format", message=ext_msg))
            return ProcessResult(
                success=False,
                input_type=InputType.VIDEO,
                is_valid=False,
                errors=errors,
                max_chars_allowed=LIMITS["text"]["max_chars"],
            )
        
        try:
            # Extract audio from video
            audio_path = self._extract_audio_from_video(file_path)
            
            if not audio_path:
                errors.append(ValidationError(
                    field="extract",
                    message="Không thể trích xuất audio từ video"
                ))
                return ProcessResult(
                    success=False,
                    input_type=InputType.VIDEO,
                    is_valid=False,
                    errors=errors,
                    max_chars_allowed=LIMITS["text"]["max_chars"],
                )
            
            # Get video duration
            video = VideoFileClip(file_path)
            duration = video.duration
            video.close()
            
            # Check duration
            dur_ok, dur_msg = validate_duration(duration, limits["max_duration_seconds"])
            if not dur_ok:
                errors.append(ValidationError(field="duration", message=dur_msg))
                self._cleanup_file(audio_path)
                return ProcessResult(
                    success=False,
                    input_type=InputType.VIDEO,
                    duration_seconds=duration,
                    is_valid=False,
                    errors=errors,
                    max_chars_allowed=LIMITS["text"]["max_chars"],
                )
            
            # Call Whisper
            result = self._call_whisper(audio_path)
            self._cleanup_file(audio_path)  # Cleanup temp audio
            
            if not result:
                errors.append(ValidationError(
                    field="whisper",
                    message="Không thể kết nối Whisper service"
                ))
                return ProcessResult(
                    success=False,
                    input_type=InputType.VIDEO,
                    is_valid=False,
                    errors=errors,
                    max_chars_allowed=LIMITS["text"]["max_chars"],
                )
            
            extracted_text = result.get("full_text", "").strip()
            lang = result.get("language", "en")
            is_english = (lang == "en")
            
            if not is_english:
                errors.append(ValidationError(
                    field="language",
                    message=f"Phát hiện ngôn ngữ: {lang.upper()}",
                    suggestion="Hệ thống chỉ hỗ trợ nội dung tiếng Anh"
                ))
            
            segments = []
            for seg in result.get("segments", []):
                segments.append(PreviewSegment(
                    id=seg["id"],
                    start_time=seg["start_time"],
                    end_time=seg["end_time"],
                    text=seg["text"],
                ))
            
            char_count = len(extracted_text)
            word_count = len(extracted_text.split())
            max_chars = LIMITS["text"]["max_chars"]
            
            if char_count > max_chars:
                over = char_count - max_chars
                warnings.append(f"Transcript vượt quá {over} ký tự.")
            
            return ProcessResult(
                success=True,
                input_type=InputType.VIDEO,
                extracted_text=extracted_text,
                word_count=word_count,
                char_count=char_count,
                duration_seconds=duration,
                language_detected=lang,
                is_english=is_english,
                segments=segments,
                is_valid=(len(errors) == 0 and is_english),
                errors=errors,
                warnings=warnings,
                max_chars_allowed=max_chars,
                chars_over_limit=max(0, char_count - max_chars),
            )
            
        except Exception as e:
            errors.append(ValidationError(field="process", message=f"Lỗi xử lý video: {str(e)}"))
            return ProcessResult(
                success=False,
                input_type=InputType.VIDEO,
                is_valid=False,
                errors=errors,
                max_chars_allowed=LIMITS["text"]["max_chars"],
            )
    
    # ==================== YOUTUBE ====================
    def process_youtube(self, url: str) -> ProcessResult:
        """Process YouTube URL: download audio then call Whisper"""
        limits = LIMITS["youtube"]
        errors = []
        warnings = []
        
        # Validate URL
        url_ok, url_msg = validate_youtube_url(url)
        if not url_ok:
            errors.append(ValidationError(field="url", message=url_msg))
            return ProcessResult(
                success=False,
                input_type=InputType.YOUTUBE,
                is_valid=False,
                errors=errors,
                max_chars_allowed=LIMITS["text"]["max_chars"],
            )
        
        try:
            # Get video info first
            info = self._get_youtube_info(url)
            if not info:
                errors.append(ValidationError(
                    field="youtube",
                    message="Không thể lấy thông tin video",
                    suggestion="Kiểm tra URL hoặc video có thể bị private/xóa"
                ))
                return ProcessResult(
                    success=False,
                    input_type=InputType.YOUTUBE,
                    is_valid=False,
                    errors=errors,
                    max_chars_allowed=LIMITS["text"]["max_chars"],
                )
            
            duration = info.get("duration", 0)
            
            # Check duration
            dur_ok, dur_msg = validate_duration(duration, limits["max_duration_seconds"])
            if not dur_ok:
                errors.append(ValidationError(field="duration", message=dur_msg))
                return ProcessResult(
                    success=False,
                    input_type=InputType.YOUTUBE,
                    duration_seconds=duration,
                    is_valid=False,
                    errors=errors,
                    max_chars_allowed=LIMITS["text"]["max_chars"],
                )
            
            # Download audio
            audio_path = self._download_youtube_audio(url)
            if not audio_path:
                errors.append(ValidationError(
                    field="download",
                    message="Không thể tải audio từ YouTube"
                ))
                return ProcessResult(
                    success=False,
                    input_type=InputType.YOUTUBE,
                    is_valid=False,
                    errors=errors,
                    max_chars_allowed=LIMITS["text"]["max_chars"],
                )
            
            # Call Whisper
            result = self._call_whisper(audio_path)
            self._cleanup_file(audio_path)
            
            if not result:
                errors.append(ValidationError(
                    field="whisper",
                    message="Không thể kết nối Whisper service"
                ))
                return ProcessResult(
                    success=False,
                    input_type=InputType.YOUTUBE,
                    is_valid=False,
                    errors=errors,
                    max_chars_allowed=LIMITS["text"]["max_chars"],
                )
            
            extracted_text = result.get("full_text", "").strip()
            lang = result.get("language", "en")
            is_english = (lang == "en")
            
            if not is_english:
                errors.append(ValidationError(
                    field="language",
                    message=f"Phát hiện ngôn ngữ: {lang.upper()}",
                    suggestion="Hệ thống chỉ hỗ trợ nội dung tiếng Anh"
                ))
            
            segments = []
            for seg in result.get("segments", []):
                segments.append(PreviewSegment(
                    id=seg["id"],
                    start_time=seg["start_time"],
                    end_time=seg["end_time"],
                    text=seg["text"],
                ))
            
            char_count = len(extracted_text)
            word_count = len(extracted_text.split())
            max_chars = LIMITS["text"]["max_chars"]
            
            if char_count > max_chars:
                over = char_count - max_chars
                warnings.append(f"Transcript vượt quá {over} ký tự.")
            
            return ProcessResult(
                success=True,
                input_type=InputType.YOUTUBE,
                extracted_text=extracted_text,
                word_count=word_count,
                char_count=char_count,
                duration_seconds=duration,
                language_detected=lang,
                is_english=is_english,
                segments=segments,
                is_valid=(len(errors) == 0 and is_english),
                errors=errors,
                warnings=warnings,
                max_chars_allowed=max_chars,
                chars_over_limit=max(0, char_count - max_chars),
            )
            
        except Exception as e:
            errors.append(ValidationError(field="process", message=f"Lỗi xử lý YouTube: {str(e)}"))
            return ProcessResult(
                success=False,
                input_type=InputType.YOUTUBE,
                is_valid=False,
                errors=errors,
                max_chars_allowed=LIMITS["text"]["max_chars"],
            )
    
    # ==================== HELPER METHODS ====================
    def _clean_text(self, text: str) -> str:
        """Clean extracted text"""
        import re
        # Remove multiple newlines
        text = re.sub(r'\n{3,}', '\n\n', text)
        # Remove multiple spaces
        text = re.sub(r' {2,}', ' ', text)
        # Remove leading/trailing whitespace per line
        lines = [line.strip() for line in text.split('\n')]
        return '\n'.join(lines).strip()
    
    def _call_whisper(self, audio_path: str) -> Optional[dict]:
        """Call Whisper STT service"""
        try:
            with open(audio_path, "rb") as f:
                response = requests.post(
                    f"{WHISPER_SERVICE_URL}/transcribe",
                    files={"file": f},
                    params={"language": "en", "word_timestamps": True},
                    timeout=300,  # 5 phút timeout
                )
            
            if response.status_code == 200:
                return response.json().get("data")
            return None
        except Exception as e:
            print(f"Whisper error: {e}")
            return None
    
    def _extract_audio_from_video(self, video_path: str) -> Optional[str]:
        """Extract audio track from video file"""
        try:
            audio_path = str(self.temp_dir / f"{uuid.uuid4()}.wav")
            video = VideoFileClip(video_path)
            video.audio.write_audiofile(audio_path, verbose=False, logger=None)
            video.close()
            return audio_path
        except Exception as e:
            print(f"Extract audio error: {e}")
            return None
    
    def _get_youtube_info(self, url: str) -> Optional[dict]:
        """Get YouTube video info without downloading"""
        try:
            ydl_opts = {
                'quiet': True,
                'no_warnings': True,
                'extract_flat': False,
            }
            with yt_dlp.YoutubeDL(ydl_opts) as ydl:
                info = ydl.extract_info(url, download=False)
                return {
                    "title": info.get("title"),
                    "duration": info.get("duration", 0),
                    "channel": info.get("channel"),
                }
        except Exception as e:
            print(f"YouTube info error: {e}")
            return None
    
    def _download_youtube_audio(self, url: str) -> Optional[str]:
        """Download audio from YouTube"""
        try:
            output_path = str(self.temp_dir / f"{uuid.uuid4()}")
            ydl_opts = {
                'format': 'bestaudio/best',
                'outtmpl': output_path,
                'postprocessors': [{
                    'key': 'FFmpegExtractAudio',
                    'preferredcodec': 'mp3',
                    'preferredquality': '128',
                }],
                'quiet': True,
                'no_warnings': True,
            }
            with yt_dlp.YoutubeDL(ydl_opts) as ydl:
                ydl.download([url])
            
            return output_path + ".mp3"
        except Exception as e:
            print(f"YouTube download error: {e}")
            return None
    
    def _cleanup_file(self, file_path: str):
        """Delete temporary file"""
        try:
            if os.path.exists(file_path):
                os.remove(file_path)
        except:
            pass