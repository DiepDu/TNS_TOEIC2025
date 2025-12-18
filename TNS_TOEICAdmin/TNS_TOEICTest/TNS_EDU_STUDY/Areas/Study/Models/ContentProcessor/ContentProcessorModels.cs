using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TNS_EDU_STUDY.Areas.Study.Models.ContentProcessor
{
    #region Enums

    /// <summary>
    /// Loại input được hỗ trợ
    /// </summary>
    public enum InputType
    {
        Text,
        Pdf,
        Docx,
        Image,
        Audio,
        Video,
        Youtube
    }

    #endregion

    #region Request Models

    /// <summary>
    /// Request gửi text trực tiếp
    /// </summary>
    public class TextInputRequest
    {
        [Required(ErrorMessage = "Nội dung không được để trống")]
        [MinLength(50, ErrorMessage = "Nội dung phải có ít nhất 50 ký tự")]
        [MaxLength(5000, ErrorMessage = "Nội dung không được vượt quá 5000 ký tự")]
        public string Content { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request gửi YouTube URL
    /// </summary>
    public class YouTubeInputRequest
    {
        [Required(ErrorMessage = "URL không được để trống")]
        [Url(ErrorMessage = "URL không hợp lệ")]
        [RegularExpression(@"^(https?://)?(www\.)?(youtube\.com|youtu\.be)/.+$",
            ErrorMessage = "Phải là URL YouTube hợp lệ")]
        public string Url { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request upload file
    /// </summary>
    public class FileInputRequest
    {
        [Required(ErrorMessage = "Vui lòng chọn file")]
        public IFormFile File { get; set; } = null!;

        [Required(ErrorMessage = "Loại file không được để trống")]
        public string InputType { get; set; } = string.Empty;
    }

    #endregion

    #region Response Models

    /// <summary>
    /// Kết quả xử lý từ Python service
    /// </summary>
    public class ProcessResult
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("input_type")]
        public string? InputType { get; set; }

        [JsonPropertyName("extracted_text")]
        public string? ExtractedText { get; set; }

        [JsonPropertyName("word_count")]
        public int WordCount { get; set; }

        [JsonPropertyName("char_count")]
        public int CharCount { get; set; }

        [JsonPropertyName("language_detected")]
        public string? LanguageDetected { get; set; }

        [JsonPropertyName("is_english")]
        public bool IsEnglish { get; set; }

        [JsonPropertyName("is_valid")]
        public bool IsValid { get; set; }

        [JsonPropertyName("duration_seconds")]
        public double? DurationSeconds { get; set; }

        [JsonPropertyName("page_count")]
        public int? PageCount { get; set; }

        [JsonPropertyName("ocr_confidence")]
        public double? OcrConfidence { get; set; }

        [JsonPropertyName("errors")]
        public List<ErrorInfo>? Errors { get; set; }

        [JsonPropertyName("warnings")]
        public List<string>? Warnings { get; set; }
    }

    /// <summary>
    /// Thông tin lỗi
    /// </summary>
    public class ErrorInfo
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    #endregion

    #region Limits Models

    /// <summary>
    /// Giới hạn cho từng loại input
    /// </summary>
    public class InputLimits
    {
        [JsonPropertyName("text")]
        public TextLimits? Text { get; set; }

        [JsonPropertyName("image")]
        public ImageLimits? Image { get; set; }

        [JsonPropertyName("pdf")]
        public PdfLimits? Pdf { get; set; }

        [JsonPropertyName("docx")]
        public DocxLimits? Docx { get; set; }

        [JsonPropertyName("audio")]
        public MediaLimits? Audio { get; set; }

        [JsonPropertyName("video")]
        public MediaLimits? Video { get; set; }

        [JsonPropertyName("youtube")]
        public YoutubeLimits? Youtube { get; set; }
    }

    public class TextLimits
    {
        [JsonPropertyName("min_chars")]
        public int MinChars { get; set; } = 50;

        [JsonPropertyName("max_chars")]
        public int MaxChars { get; set; } = 5000;
    }

    public class ImageLimits
    {
        [JsonPropertyName("max_size_mb")]
        public double MaxSizeMb { get; set; } = 5;

        [JsonPropertyName("allowed_extensions")]
        public List<string>? AllowedExtensions { get; set; }

        [JsonPropertyName("allowed_formats")]
        public List<string>? AllowedFormats { get; set; }

        [JsonPropertyName("max_chars")]
        public int MaxChars { get; set; } = 5000;
    }

    public class PdfLimits
    {
        [JsonPropertyName("max_size_mb")]
        public double MaxSizeMb { get; set; } = 5;

        [JsonPropertyName("max_pages")]
        public int MaxPages { get; set; } = 10;

        [JsonPropertyName("allowed_extensions")]
        public List<string>? AllowedExtensions { get; set; }

        [JsonPropertyName("max_chars")]
        public int MaxChars { get; set; } = 5000;
    }

    public class DocxLimits
    {
        [JsonPropertyName("max_size_mb")]
        public double MaxSizeMb { get; set; } = 5;

        [JsonPropertyName("allowed_extensions")]
        public List<string>? AllowedExtensions { get; set; }

        [JsonPropertyName("max_chars")]
        public int MaxChars { get; set; } = 5000;
    }

    public class MediaLimits
    {
        [JsonPropertyName("max_size_mb")]
        public double MaxSizeMb { get; set; }

        [JsonPropertyName("max_duration_minutes")]
        public int MaxDurationMinutes { get; set; }

        [JsonPropertyName("max_duration_seconds")]
        public int MaxDurationSeconds { get; set; }

        [JsonPropertyName("allowed_extensions")]
        public List<string>? AllowedExtensions { get; set; }

        [JsonPropertyName("allowed_formats")]
        public List<string>? AllowedFormats { get; set; }
    }

    public class YoutubeLimits
    {
        [JsonPropertyName("max_duration_minutes")]
        public int MaxDurationMinutes { get; set; } = 15;

        [JsonPropertyName("max_duration_seconds")]
        public int MaxDurationSeconds { get; set; } = 900;
    }

    #endregion
}