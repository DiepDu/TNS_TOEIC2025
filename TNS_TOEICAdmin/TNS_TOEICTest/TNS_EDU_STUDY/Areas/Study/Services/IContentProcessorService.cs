using System.IO;
using System.Threading.Tasks;
using TNS_EDU_STUDY.Areas.Study.Models.ContentProcessor;

namespace TNS_EDU_STUDY.Areas.Study.Services
{
    public interface IContentProcessorService
    {
        Task<bool> CheckHealthAsync();
        Task<InputLimits?> GetLimitsAsync();
        Task<ProcessResult?> ProcessTextAsync(string content);
        Task<ProcessResult?> ProcessFileAsync(Stream fileStream, string fileName, string inputType);
        Task<ProcessResult?> ProcessYouTubeAsync(string url);
    }
}