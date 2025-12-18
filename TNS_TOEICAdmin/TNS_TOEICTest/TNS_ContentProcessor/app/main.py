from fastapi import FastAPI, File, UploadFile, Form
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import Optional, List
import os
import tempfile
import re
import uvicorn
import shutil

# Document processing
import pdfplumber
from docx import Document

# ═══════════════════════════════════════════════════════════════
# CONSTANTS - Gemini 2.5 Pro free tier limits
# ═══════════════════════════════════════════════════════════════
MAX_CHARS = 5000
MIN_CHARS = 50
MAX_DURATION_MINUTES = 15  # Tăng lên 15 phút theo đặc tả

# ═══════════════════════════════════════════════════════════════
# FFmpeg PATH fix - Ensure FFmpeg is accessible
# ═══════════════════════════════════════════════════════════════
def setup_ffmpeg():
    """Ensure FFmpeg is in PATH"""
    if shutil.which("ffmpeg"):
        return shutil.which("ffmpeg")
    
    possible_paths = [
        r"C:\ffmpeg\bin",
        r"C:\ffmpeg\ffmpeg-7.1-essentials_build\bin",
        r"C:\Program Files\ffmpeg\bin",
        r"C:\ProgramData\chocolatey\bin",
        r"C:\tools\ffmpeg\bin",
    ]
    
    for base in [r"C:\\", r"D:\\"]:
        if os.path.exists(base):
            try:
                for item in os.listdir(base):
                    if "ffmpeg" in item.lower():
                        bin_path = os.path.join(base, item, "bin")
                        if os.path.exists(os.path.join(bin_path, "ffmpeg.exe")):
                            possible_paths.insert(0, bin_path)
            except:
                pass
    
    for path in possible_paths:
        ffmpeg_exe = os.path.join(path, "ffmpeg.exe")
        if os.path.exists(ffmpeg_exe):
            os.environ["PATH"] = path + ";" + os.environ.get("PATH", "")
            return ffmpeg_exe
    
    return None

FFMPEG_PATH = setup_ffmpeg()
FFMPEG_AVAILABLE = FFMPEG_PATH is not None

# ═══════════════════════════════════════════════════════════════
# OPTIONAL IMPORTS
# ═══════════════════════════════════════════════════════════════

# OCR (EasyOCR)
OCR_AVAILABLE = False
OCR_READER = None
try:
    import easyocr
    OCR_AVAILABLE = True
    def get_ocr_reader():
        global OCR_READER
        if OCR_READER is None:
            OCR_READER = easyocr.Reader(['en'], gpu=False, verbose=False)
        return OCR_READER
except ImportError as e:
    print(f"[WARNING] EasyOCR not available: {e}")

# Whisper for Audio/Video
WHISPER_AVAILABLE = False
WHISPER_MODEL = None
WHISPER_ERROR = None
try:
    import whisper
    WHISPER_AVAILABLE = True
    def get_whisper_model():
        global WHISPER_MODEL
        if WHISPER_MODEL is None:
            WHISPER_MODEL = whisper.load_model("base")
        return WHISPER_MODEL
except ImportError as e:
    WHISPER_ERROR = f"Import error: {e}"
    print(f"[WARNING] Whisper not available: {e}")
except Exception as e:
    WHISPER_ERROR = f"Error: {e}"
    print(f"[WARNING] Whisper error: {e}")

# yt-dlp for YouTube audio download
YTDLP_AVAILABLE = False
try:
    import yt_dlp
    YTDLP_AVAILABLE = True
except ImportError as e:
    print(f"[WARNING] yt-dlp not available: {e}")

# moviepy for video audio extraction
MOVIEPY_AVAILABLE = False
try:
    from moviepy.editor import VideoFileClip
    MOVIEPY_AVAILABLE = True
except ImportError as e:
    print(f"[WARNING] moviepy not available: {e}")

# YouTube transcript (fallback)
YOUTUBE_AVAILABLE = False
try:
    from youtube_transcript_api import YouTubeTranscriptApi
    YOUTUBE_AVAILABLE = True
except ImportError as e:
    print(f"[WARNING] YouTube API not available: {e}")

# Language detection
LANGDETECT_AVAILABLE = False
try:
    from langdetect import detect
    LANGDETECT_AVAILABLE = True
except ImportError:
    pass

app = FastAPI(title="TNS Content Processor", version="2.4.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# ═══════════════════════════════════════════════════════════════
# MODELS
# ═══════════════════════════════════════════════════════════════

class ValidationError(BaseModel):
    field: str
    message: str
    suggestion: Optional[str] = None

class TextRequest(BaseModel):
    content: str

class YouTubeRequest(BaseModel):
    url: str

class ProcessResult(BaseModel):
    success: bool
    input_type: Optional[str] = None
    extracted_text: Optional[str] = ""
    word_count: Optional[int] = 0
    char_count: Optional[int] = 0
    page_count: Optional[int] = None
    duration_seconds: Optional[float] = None
    language_detected: Optional[str] = None
    is_english: Optional[bool] = None
    is_valid: Optional[bool] = False
    ocr_confidence: Optional[float] = None
    errors: Optional[List[ValidationError]] = []
    warnings: Optional[List[str]] = []
    max_chars_allowed: Optional[int] = MAX_CHARS
    chars_over_limit: Optional[int] = 0

class TextLimits(BaseModel):
    min_chars: int = MIN_CHARS
    max_chars: int = MAX_CHARS

class ImageLimits(BaseModel):
    max_size_mb: float = 5.0
    allowed_formats: List[str] = ["jpg", "jpeg", "png", "bmp", "tiff", "webp", "gif"]

class FileLimits(BaseModel):
    max_size_mb: float = 5.0
    max_pages: int = 10

class MediaLimits(BaseModel):
    max_size_mb: float = 25.0
    max_duration_minutes: int = MAX_DURATION_MINUTES
    allowed_formats: List[str] = []

class YoutubeLimits(BaseModel):
    max_duration_minutes: int = MAX_DURATION_MINUTES

class InputLimits(BaseModel):
    text: TextLimits = TextLimits()
    image: ImageLimits = ImageLimits()
    pdf: FileLimits = FileLimits()
    docx: FileLimits = FileLimits()
    audio: MediaLimits = MediaLimits(max_size_mb=25.0, max_duration_minutes=MAX_DURATION_MINUTES, allowed_formats=["mp3", "wav", "m4a", "ogg", "flac"])
    video: MediaLimits = MediaLimits(max_size_mb=50.0, max_duration_minutes=MAX_DURATION_MINUTES, allowed_formats=["mp4", "mov", "avi", "mkv", "webm"])
    youtube: YoutubeLimits = YoutubeLimits()

# ═══════════════════════════════════════════════════════════════
# TEXT CLEANING FUNCTIONS
# ═══════════════════════════════════════════════════════════════

def clean_text(text: str) -> str:
    """
    Làm sạch văn bản:
    - Xóa header/footer lặp lại
    - Sửa hyphenation (từ bị ngắt dòng)
    - Chuẩn hóa khoảng trắng
    - Xóa ký tự đặc biệt không cần thiết
    """
    if not text:
        return ""
    
    # 1. Sửa hyphenation (từ bị ngắt dòng)
    # Pattern: từ- \n tiếp_tục → từtiếp_tục
    text = re.sub(r'(\w+)-\s*\n\s*(\w+)', r'\1\2', text)
    
    # 2. Xóa page numbers đứng riêng một dòng
    text = re.sub(r'^\s*\d+\s*$', '', text, flags=re.MULTILINE)
    text = re.sub(r'^\s*Page\s+\d+\s*(of\s+\d+)?\s*$', '', text, flags=re.MULTILINE | re.IGNORECASE)
    
    # 3. Xóa header/footer patterns phổ biến
    # Pattern: dòng lặp lại nhiều lần (có thể là header/footer)
    lines = text.split('\n')
    if len(lines) > 10:
        # Đếm tần suất xuất hiện của mỗi dòng
        line_counts = {}
        for line in lines:
            stripped = line.strip()
            if stripped and len(stripped) < 100:  # Header/footer thường ngắn
                line_counts[stripped] = line_counts.get(stripped, 0) + 1
        
        # Xóa các dòng xuất hiện > 3 lần (likely header/footer)
        repeated_lines = {line for line, count in line_counts.items() if count > 3}
        lines = [line for line in lines if line.strip() not in repeated_lines]
        text = '\n'.join(lines)
    
    # 4. Chuẩn hóa khoảng trắng
    text = re.sub(r'[ \t]+', ' ', text)  # Multiple spaces → single space
    text = re.sub(r'\n{3,}', '\n\n', text)  # Multiple newlines → double newline
    
    # 5. Xóa ký tự điều khiển và ký tự lạ
    text = re.sub(r'[\x00-\x08\x0b\x0c\x0e-\x1f\x7f-\x9f]', '', text)
    
    # 6. Xóa bullet points và numbering thừa
    text = re.sub(r'^\s*[•●○◦▪▫]\s*', '', text, flags=re.MULTILINE)
    
    # 7. Trim
    text = text.strip()
    
    return text


def clean_transcript(text: str) -> str:
    """
    Làm sạch transcript từ YouTube hoặc Whisper:
    - Xóa [Music], [Applause], etc.
    - Chuẩn hóa câu
    """
    if not text:
        return ""
    
    # Xóa các annotation trong ngoặc vuông
    text = re.sub(r'\[.*?\]', '', text)
    
    # Xóa các filler words phổ biến (tùy chọn, có thể comment nếu không muốn)
    # text = re.sub(r'\b(um|uh|er|ah|like|you know)\b', '', text, flags=re.IGNORECASE)
    
    # Chuẩn hóa khoảng trắng
    text = re.sub(r'\s+', ' ', text)
    
    # Capitalize đầu câu
    sentences = re.split(r'([.!?]+)', text)
    result = []
    for i, part in enumerate(sentences):
        if i % 2 == 0 and part.strip():  # Sentence content
            part = part.strip()
            if part:
                part = part[0].upper() + part[1:] if len(part) > 1 else part.upper()
        result.append(part)
    text = ''.join(result)
    
    return text.strip()


# ═══════════════════════════════════════════════════════════════
# HELPER FUNCTIONS
# ═══════════════════════════════════════════════════════════════

def detect_language(text: str) -> tuple:
    if not text or len(text.strip()) < 10:
        return "unknown", False
    if LANGDETECT_AVAILABLE:
        try:
            lang = detect(text)
            return lang, lang == "en"
        except:
            pass
    english_words = ["the", "is", "are", "was", "were", "have", "has", "been", "will", "would", "could", "should", "can", "may"]
    text_lower = text.lower()
    english_count = sum(1 for word in english_words if f" {word} " in f" {text_lower} ")
    return ("en", True) if english_count >= 2 else ("unknown", False)


def validate_content(text: str, max_chars: int = MAX_CHARS, min_chars: int = MIN_CHARS) -> tuple:
    errors, warnings = [], []
    if not text or not text.strip():
        errors.append(ValidationError(field="content", message="No text content found", suggestion="Please check your input file"))
        return False, errors, warnings
    
    char_count = len(text)
    if char_count < min_chars:
        errors.append(ValidationError(field="content", message=f"Content too short ({char_count} characters)", suggestion=f"Minimum {min_chars} characters required"))
    if char_count > max_chars:
        warnings.append(f"Content has {char_count} characters, exceeds limit of {max_chars}.")
    
    lang, is_english = detect_language(text)
    if not is_english and lang != "unknown":
        warnings.append(f"Detected language: {lang.upper()}. Content should be in English.")
    
    return len(errors) == 0, errors, warnings


def extract_pdf(file_path: str) -> tuple:
    errors, text_parts = [], []
    try:
        with pdfplumber.open(file_path) as pdf:
            page_count = len(pdf.pages)
            if page_count > 10:
                errors.append(ValidationError(field="file", message=f"PDF has {page_count} pages, exceeds 10 page limit", suggestion="Please use a PDF with max 10 pages"))
                return "", page_count, errors
            for page in pdf.pages:
                if page_text := page.extract_text():
                    text_parts.append(page_text.strip())
        
        full_text = "\n\n".join(text_parts)
        # Làm sạch text
        full_text = clean_text(full_text)
        
        return full_text, page_count, errors
    except Exception as e:
        errors.append(ValidationError(field="file", message=f"Error reading PDF: {str(e)}", suggestion="Please check your PDF file"))
        return "", 0, errors


def extract_docx(file_path: str) -> tuple:
    errors = []
    try:
        doc = Document(file_path)
        paragraphs = [p.text.strip() for p in doc.paragraphs if p.text.strip()]
        full_text = "\n\n".join(paragraphs)
        
        # Làm sạch text
        full_text = clean_text(full_text)
        
        return full_text, max(1, len(full_text) // 3000 + 1), errors
    except Exception as e:
        errors.append(ValidationError(field="file", message=f"Error reading DOCX: {str(e)}", suggestion="Please check your Word file"))
        return "", 0, errors


def extract_image_ocr(file_path: str) -> tuple:
    errors = []
    if not OCR_AVAILABLE:
        errors.append(ValidationError(field="ocr", message="OCR not installed", suggestion="pip install easyocr"))
        return "", 0.0, errors
    
    try:
        reader = get_ocr_reader()
        results = reader.readtext(file_path)
        
        if not results:
            errors.append(ValidationError(field="content", message="No text detected in image", suggestion="Use a clearer image"))
            return "", 0.0, errors
        
        texts, confidences = [], []
        for (_, text, conf) in results:
            if text.strip():
                texts.append(text.strip())
                confidences.append(conf)
        
        full_text = " ".join(texts)
        avg_conf = sum(confidences) / len(confidences) if confidences else 0.0
        
        # Làm sạch text
        full_text = clean_text(full_text)
        
        return full_text, avg_conf, errors
    except Exception as e:
        errors.append(ValidationError(field="ocr", message=f"OCR error: {str(e)}", suggestion="Try another image"))
        return "", 0.0, errors


def extract_audio_from_video(video_path: str) -> tuple:
    """Tách audio từ video file sử dụng moviepy"""
    if not MOVIEPY_AVAILABLE:
        return None, "moviepy not installed"
    
    try:
        audio_path = video_path.rsplit('.', 1)[0] + '_audio.wav'
        
        video = VideoFileClip(video_path)
        if video.audio is None:
            video.close()
            return None, "Video has no audio track"
        
        video.audio.write_audiofile(audio_path, verbose=False, logger=None)
        video.close()
        
        return audio_path, None
    except Exception as e:
        return None, str(e)


def extract_audio_whisper(file_path: str, is_video: bool = False) -> tuple:
    """Extract text from audio/video using Whisper"""
    errors = []
    max_duration = MAX_DURATION_MINUTES * 60
    
    if not FFMPEG_AVAILABLE:
        errors.append(ValidationError(
            field="ffmpeg", 
            message="FFmpeg not found", 
            suggestion="Install FFmpeg and restart: winget install FFmpeg"
        ))
        return "", 0.0, errors
    
    if not WHISPER_AVAILABLE:
        error_msg = WHISPER_ERROR or "Whisper not installed"
        errors.append(ValidationError(
            field="whisper", 
            message=error_msg, 
            suggestion="pip install openai-whisper"
        ))
        return "", 0.0, errors
    
    if not os.path.exists(file_path):
        errors.append(ValidationError(field="file", message="File not found", suggestion="Upload failed"))
        return "", 0.0, errors
    
    audio_path = file_path
    temp_audio = None
    
    try:
        # Nếu là video và có moviepy, tách audio trước
        if is_video and MOVIEPY_AVAILABLE:
            print(f"[DEBUG] Extracting audio from video: {file_path}")
            extracted_audio, extract_error = extract_audio_from_video(file_path)
            if extracted_audio:
                audio_path = extracted_audio
                temp_audio = extracted_audio
                print(f"[DEBUG] Audio extracted to: {audio_path}")
            elif extract_error:
                print(f"[WARNING] Could not extract audio: {extract_error}, using original file")
        
        print(f"[DEBUG] Processing audio: {audio_path}")
        print(f"[DEBUG] File size: {os.path.getsize(audio_path)} bytes")
        
        model = get_whisper_model()
        result = model.transcribe(audio_path, language="en", fp16=False)
        
        text = result.get("text", "").strip()
        segments = result.get("segments", [])
        duration = segments[-1].get("end", 0.0) if segments else 0.0
        
        # Làm sạch transcript
        text = clean_transcript(text)
        
        print(f"[DEBUG] Transcription complete: {len(text)} chars, {duration:.1f}s")
        
        if duration > max_duration:
            errors.append(ValidationError(
                field="duration", 
                message=f"Audio is {duration/60:.1f} minutes, exceeds {MAX_DURATION_MINUTES} minute limit", 
                suggestion="Use shorter audio"
            ))
        if not text:
            errors.append(ValidationError(field="content", message="No speech detected", suggestion="Ensure audio has clear speech"))
        
        return text, duration, errors
        
    except Exception as e:
        error_msg = str(e)
        print(f"[ERROR] Whisper error: {error_msg}")
        
        if "ffmpeg" in error_msg.lower() or "WinError 2" in error_msg:
            errors.append(ValidationError(
                field="ffmpeg", 
                message="FFmpeg not accessible to Python", 
                suggestion="Close VS Code completely, reopen, then run service again"
            ))
        else:
            errors.append(ValidationError(field="whisper", message=f"Error: {error_msg}", suggestion="Try another file"))
        return "", 0.0, errors
    
    finally:
        # Cleanup temp audio file
        if temp_audio and os.path.exists(temp_audio):
            try:
                os.unlink(temp_audio)
            except:
                pass


def download_youtube_audio(url: str, output_dir: str) -> tuple:
    """Download audio từ YouTube sử dụng yt-dlp"""
    if not YTDLP_AVAILABLE:
        return None, None, "yt-dlp not installed"
    
    try:
        output_path = os.path.join(output_dir, 'yt_audio.%(ext)s')
        
        ydl_opts = {
            'format': 'bestaudio[ext=m4a]/bestaudio/best',
            'outtmpl': output_path,
            'quiet': True,
            'no_warnings': True,
            'extract_flat': False,
        }
        
        with yt_dlp.YoutubeDL(ydl_opts) as ydl:
            # Lấy info trước để check duration
            info = ydl.extract_info(url, download=False)
            duration = info.get('duration', 0)
            title = info.get('title', 'Unknown')
            
            if duration > MAX_DURATION_MINUTES * 60:
                return None, duration, f"Video is {duration/60:.1f} minutes, exceeds {MAX_DURATION_MINUTES} minute limit"
            
            # Download audio
            ydl.download([url])
            
            # Tìm file đã download
            for ext in ['m4a', 'webm', 'mp3', 'wav', 'opus']:
                audio_file = os.path.join(output_dir, f'yt_audio.{ext}')
                if os.path.exists(audio_file):
                    return audio_file, duration, None
        
        return None, duration, "Could not find downloaded audio file"
        
    except Exception as e:
        return None, 0, str(e)


def extract_youtube_video_id(url: str) -> Optional[str]:
    patterns = [
        r'(?:youtube\.com\/watch\?v=|youtu\.be\/|youtube\.com\/embed\/)([a-zA-Z0-9_-]{11})',
        r'youtube\.com\/shorts\/([a-zA-Z0-9_-]{11})',
        r'[?&]v=([a-zA-Z0-9_-]{11})',
    ]
    for pattern in patterns:
        if match := re.search(pattern, url):
            return match.group(1)
    return None


def extract_youtube_transcript(url: str) -> tuple:
    """
    Extract transcript từ YouTube:
    1. Thử dùng yt-dlp + Whisper (chính xác hơn)
    2. Fallback: dùng YouTube transcript API
    """
    errors, warnings = [], []
    max_duration = MAX_DURATION_MINUTES * 60
    
    video_id = extract_youtube_video_id(url)
    if not video_id:
        errors.append(ValidationError(field="url", message="Invalid YouTube URL", suggestion="Use URL format: youtube.com/watch?v=xxxxx"))
        return "", 0.0, errors, warnings
    
    # Method 1: yt-dlp + Whisper (nếu có)
    if YTDLP_AVAILABLE and WHISPER_AVAILABLE and FFMPEG_AVAILABLE:
        print(f"[DEBUG] Trying yt-dlp + Whisper for: {url}")
        
        with tempfile.TemporaryDirectory() as tmp_dir:
            audio_path, duration, download_error = download_youtube_audio(url, tmp_dir)
            
            if audio_path and os.path.exists(audio_path):
                print(f"[DEBUG] Downloaded audio: {audio_path}")
                
                try:
                    model = get_whisper_model()
                    result = model.transcribe(audio_path, language="en", fp16=False)
                    
                    text = result.get("text", "").strip()
                    segments = result.get("segments", [])
                    actual_duration = segments[-1].get("end", 0.0) if segments else duration
                    
                    if text:
                        text = clean_transcript(text)
                        warnings.append("Transcribed using Whisper (high accuracy)")
                        return text, actual_duration, errors, warnings
                        
                except Exception as e:
                    print(f"[WARNING] Whisper failed: {e}, falling back to transcript API")
            
            elif download_error:
                if "exceeds" in download_error:
                    errors.append(ValidationError(field="duration", message=download_error, suggestion="Use a shorter video"))
                    return "", 0.0, errors, warnings
                print(f"[WARNING] yt-dlp failed: {download_error}, falling back to transcript API")
    
    # Method 2: YouTube Transcript API (fallback)
    if not YOUTUBE_AVAILABLE:
        errors.append(ValidationError(field="youtube", message="YouTube processing not available", suggestion="Install: pip install youtube-transcript-api yt-dlp"))
        return "", 0.0, errors, warnings
    
    print(f"[DEBUG] Using YouTube Transcript API for: {video_id}")
    
    transcript_data = None
    
    try:
        # Try NEW API first (v1.0+)
        try:
            ytt_api = YouTubeTranscriptApi()
            try:
                transcript_data = ytt_api.fetch(video_id, languages=['en'])
            except:
                pass
            if not transcript_data:
                try:
                    transcript_data = ytt_api.fetch(video_id, languages=['en-US', 'en-GB', 'en-AU'])
                    warnings.append("Using auto-generated English subtitles")
                except:
                    pass
            if not transcript_data:
                try:
                    transcript_list = ytt_api.list(video_id)
                    for t in transcript_list:
                        transcript_data = t.fetch()
                        warnings.append(f"Using subtitles: {getattr(t, 'language', 'Unknown')}")
                        break
                except:
                    pass
        except TypeError:
            # Fall back to OLD API
            try:
                transcript_data = YouTubeTranscriptApi.get_transcript(video_id, languages=['en'])
            except:
                pass
            if not transcript_data:
                try:
                    transcript_data = YouTubeTranscriptApi.get_transcript(video_id, languages=['en-US', 'en-GB'])
                    warnings.append("Using auto-generated English subtitles")
                except:
                    pass
            if not transcript_data:
                try:
                    transcript_list = YouTubeTranscriptApi.list_transcripts(video_id)
                    for t in transcript_list:
                        transcript_data = t.fetch()
                        warnings.append(f"Using subtitles: {t.language}")
                        break
                except:
                    pass
        
        if not transcript_data:
            errors.append(ValidationError(field="transcript", message="No subtitles found", suggestion="Choose a video with CC enabled"))
            return "", 0.0, errors, warnings
        
        texts, duration = [], 0.0
        for entry in transcript_data:
            if hasattr(entry, 'text'):
                text, start, dur = entry.text, getattr(entry, 'start', 0), getattr(entry, 'duration', 0)
            else:
                text, start, dur = entry.get('text', ''), entry.get('start', 0), entry.get('duration', 0)
            if text:
                texts.append(text)
            if start + dur > duration:
                duration = start + dur
        
        full_text = " ".join(texts)
        
        # Làm sạch transcript
        full_text = clean_transcript(full_text)
        
        if duration > max_duration:
            errors.append(ValidationError(field="duration", message=f"Video is {duration/60:.1f} minutes, exceeds limit", suggestion="Use shorter video"))
        if not full_text:
            errors.append(ValidationError(field="content", message="Subtitles empty", suggestion="Choose video with spoken content"))
        
        return full_text, duration, errors, warnings
        
    except Exception as e:
        error_msg = str(e)
        if "disabled" in error_msg.lower():
            errors.append(ValidationError(field="transcript", message="Subtitles disabled", suggestion="Choose video with CC enabled"))
        else:
            errors.append(ValidationError(field="youtube", message=f"Error: {error_msg}", suggestion="Try another video"))
    
    return "", 0.0, errors, warnings


# ═══════════════════════════════════════════════════════════════
# ENDPOINTS
# ═══════════════════════════════════════════════════════════════

@app.get("/health")
async def health():
    return {
        "status": "healthy",
        "service": "TNS_ContentProcessor",
        "version": "2.4.0",
        "features": {
            "text": True, "pdf": True, "docx": True,
            "image_ocr": OCR_AVAILABLE,
            "audio_whisper": WHISPER_AVAILABLE,
            "video_whisper": WHISPER_AVAILABLE,
            "video_moviepy": MOVIEPY_AVAILABLE,
            "youtube_transcript": YOUTUBE_AVAILABLE,
            "youtube_ytdlp": YTDLP_AVAILABLE,
            "ffmpeg": FFMPEG_AVAILABLE,
            "text_cleaning": True,
        },
        "ffmpeg_path": FFMPEG_PATH,
        "whisper_error": WHISPER_ERROR,
        "limits": {"max_chars": MAX_CHARS, "max_duration_minutes": MAX_DURATION_MINUTES}
    }

@app.get("/limits")
async def get_limits():
    return InputLimits()

@app.post("/process/text")
async def process_text(request: TextRequest):
    content = request.content.strip()
    
    # Làm sạch text
    content = clean_text(content)
    
    lang, is_english = detect_language(content)
    is_valid, errors, warnings = validate_content(content)
    return ProcessResult(
        success=True, input_type="text", extracted_text=content,
        word_count=len(content.split()), char_count=len(content),
        language_detected=lang, is_english=is_english, is_valid=is_valid,
        errors=errors, warnings=warnings, max_chars_allowed=MAX_CHARS, 
        chars_over_limit=max(0, len(content) - MAX_CHARS)
    )

@app.post("/process/file")
async def process_file(file: UploadFile = File(...), input_type: str = Form(...)):
    try:
        suffix = os.path.splitext(file.filename)[1].lower() if file.filename else ".tmp"
        with tempfile.NamedTemporaryFile(delete=False, suffix=suffix) as tmp:
            content = await file.read()
            tmp.write(content)
            tmp_path = tmp.name
        
        file_size_mb = len(content) / (1024 * 1024)
        
        extracted_text, page_count, duration_seconds, ocr_confidence = "", None, None, None
        errors, warnings = [], []
        input_type_lower = input_type.lower()
        
        if input_type_lower == "pdf":
            if file_size_mb > 5:
                errors.append(ValidationError(field="file", message=f"PDF too large ({file_size_mb:.1f}MB)", suggestion="Max 5MB"))
            else:
                extracted_text, page_count, pdf_errors = extract_pdf(tmp_path)
                errors.extend(pdf_errors)
        
        elif input_type_lower == "docx":
            if file_size_mb > 5:
                errors.append(ValidationError(field="file", message=f"DOCX too large ({file_size_mb:.1f}MB)", suggestion="Max 5MB"))
            else:
                extracted_text, page_count, docx_errors = extract_docx(tmp_path)
                errors.extend(docx_errors)
        
        elif input_type_lower == "image":
            if file_size_mb > 5:
                errors.append(ValidationError(field="file", message=f"Image too large ({file_size_mb:.1f}MB)", suggestion="Max 5MB"))
            else:
                extracted_text, ocr_confidence, ocr_errors = extract_image_ocr(tmp_path)
                errors.extend(ocr_errors)
        
        elif input_type_lower == "audio":
            if file_size_mb > 25:
                errors.append(ValidationError(field="file", message=f"Audio too large ({file_size_mb:.1f}MB)", suggestion="Max 25MB"))
            else:
                extracted_text, duration_seconds, audio_errors = extract_audio_whisper(tmp_path, is_video=False)
                errors.extend(audio_errors)
        
        elif input_type_lower == "video":
            if file_size_mb > 50:
                errors.append(ValidationError(field="file", message=f"Video too large ({file_size_mb:.1f}MB)", suggestion="Max 50MB"))
            else:
                extracted_text, duration_seconds, video_errors = extract_audio_whisper(tmp_path, is_video=True)
                errors.extend(video_errors)
        
        else:
            errors.append(ValidationError(field="input_type", message=f"Unsupported: {input_type}", suggestion="Use: pdf, docx, image, audio, video"))
        
        try: 
            os.unlink(tmp_path)
        except: 
            pass
        
        word_count = len(extracted_text.split()) if extracted_text else 0
        char_count = len(extracted_text) if extracted_text else 0
        lang, is_english = detect_language(extracted_text) if extracted_text else ("unknown", False)
        
        if not errors and extracted_text:
            is_valid, val_errors, val_warnings = validate_content(extracted_text)
            errors.extend(val_errors)
            warnings.extend(val_warnings)
        else:
            is_valid = False
        
        return ProcessResult(
            success=True, input_type=input_type_lower, extracted_text=extracted_text,
            word_count=word_count, char_count=char_count, page_count=page_count,
            duration_seconds=duration_seconds, ocr_confidence=ocr_confidence,
            language_detected=lang, is_english=is_english, is_valid=is_valid and len(errors) == 0,
            errors=errors, warnings=warnings, max_chars_allowed=MAX_CHARS, 
            chars_over_limit=max(0, char_count - MAX_CHARS)
        )
        
    except Exception as e:
        return ProcessResult(
            success=False, input_type=input_type, 
            errors=[ValidationError(field="system", message=str(e), suggestion="Please try again")]
        )

@app.post("/process/youtube")
async def process_youtube(request: YouTubeRequest):
    extracted_text, duration_seconds, errors, warnings = extract_youtube_transcript(request.url)
    
    word_count = len(extracted_text.split()) if extracted_text else 0
    char_count = len(extracted_text) if extracted_text else 0
    lang, is_english = detect_language(extracted_text) if extracted_text else ("unknown", False)
    
    if not errors and extracted_text:
        is_valid, val_errors, val_warnings = validate_content(extracted_text)
        errors.extend(val_errors)
        warnings.extend(val_warnings)
    else:
        is_valid = False
    
    return ProcessResult(
        success=True, input_type="youtube", extracted_text=extracted_text,
        word_count=word_count, char_count=char_count, duration_seconds=duration_seconds,
        language_detected=lang, is_english=is_english, is_valid=is_valid and len(errors) == 0,
        errors=errors, warnings=warnings, max_chars_allowed=MAX_CHARS, 
        chars_over_limit=max(0, char_count - MAX_CHARS)
    )

@app.get("/test/youtube/{video_id}")
async def test_youtube(video_id: str):
    url = f"https://www.youtube.com/watch?v={video_id}"
    text, duration, errors, warnings = extract_youtube_transcript(url)
    return {
        "video_id": video_id, "text_length": len(text),
        "text_preview": text[:500] if text else None, "duration": duration,
        "errors": [e.dict() for e in errors], "warnings": warnings
    }


if __name__ == "__main__":
    print("=" * 60)
    print("  TNS Content Processor v2.4.0")
    print("=" * 60)
    print(f"  Text/PDF/DOCX: OK (with text cleaning)")
    print(f"  Image OCR:     {'OK' if OCR_AVAILABLE else 'Not installed (pip install easyocr)'}")
    print(f"  FFmpeg:        {FFMPEG_PATH if FFMPEG_AVAILABLE else 'Not found (winget install FFmpeg)'}")
    print(f"  Audio/Video:   {'OK' if WHISPER_AVAILABLE else f'Not available - {WHISPER_ERROR}'}")
    print(f"  Video Extract: {'OK (moviepy)' if MOVIEPY_AVAILABLE else 'Not installed (pip install moviepy)'}")
    print(f"  YouTube:       {'OK' if YOUTUBE_AVAILABLE else 'Not installed'}")
    print(f"  YouTube DL:    {'OK (yt-dlp + Whisper)' if YTDLP_AVAILABLE and WHISPER_AVAILABLE else 'Transcript API only'}")
    print(f"  Max Duration:  {MAX_DURATION_MINUTES} minutes")
    print("=" * 60)
    
    uvicorn.run("main:app", host="0.0.0.0", port=5004, reload=True)