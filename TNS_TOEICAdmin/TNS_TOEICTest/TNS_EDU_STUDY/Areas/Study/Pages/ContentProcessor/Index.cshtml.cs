using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TNS_EDU_STUDY.Areas.Study.Models.ContentProcessor;
using TNS_EDU_STUDY.Areas.Study.Services;

namespace TNS_EDU_STUDY.Areas.Study.Pages.ContentProcessor
{
    public class IndexModel : PageModel
    {
        private readonly IContentProcessorService _contentProcessorService;
        private readonly ILogger<IndexModel> _logger;

        public bool IsServiceAvailable { get; set; }
        public InputLimits? Limits { get; set; }

        public IndexModel(IContentProcessorService contentProcessorService, ILogger<IndexModel> logger)
        {
            _contentProcessorService = contentProcessorService;
            _logger = logger;
        }

        public async Task OnGetAsync()
        {
            try
            {
                IsServiceAvailable = await _contentProcessorService.CheckHealthAsync();
                if (IsServiceAvailable)
                {
                    Limits = await _contentProcessorService.GetLimitsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Content Processor service");
                IsServiceAvailable = false;
            }
        }
    }
}