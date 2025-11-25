using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using TNS_EDU_TEST.Areas.Test.Models; 

namespace TNS_EDU_STUDY.Areas.Study.Pages 
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class StudyHistoryModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public int PartSelect { get; set; } = 1;   
        public List<StudyHistoryItem> StudyHistoryItems { get; set; }
        public string StudyHistoryJson { get; set; }
        public void OnGet()
        {
         
            string memberKey = User.Claims.FirstOrDefault(c => c.Type == "MemberKey" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(memberKey))
            {
              
                StudyHistoryItems = new List<StudyHistoryItem>();
                StudyHistoryJson = "[]";
                return;
            }

          
            StudyHistoryItems = StudyHistoryAccessData.LoadStudyHistory(memberKey, this.PartSelect);

         
            var sortedHistory = StudyHistoryItems.OrderBy(item => item.CreatedOn).ToList();
            StudyHistoryJson = JsonSerializer.Serialize(sortedHistory, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
        }

        public IActionResult OnGetHistoryByPart(int part)
        {
            string memberKey = User.Claims.FirstOrDefault(c => c.Type == "MemberKey" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(memberKey))
            {
                return Unauthorized();
            }

            var historyItems = StudyHistoryAccessData.LoadStudyHistory(memberKey, part);

            var sortedHistory = historyItems.OrderBy(item => item.CreatedOn).ToList();

            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            return new JsonResult(sortedHistory, serializerOptions);
        }
    }
}
