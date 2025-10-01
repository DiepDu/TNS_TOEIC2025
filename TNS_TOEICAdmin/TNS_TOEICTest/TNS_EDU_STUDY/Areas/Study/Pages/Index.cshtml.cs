using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TNS_EDU_STUDY.Areas.Study.Models;
using TNS_EDU_TEST.Areas.Test.Models;

namespace TNS_EDU_STUDY.Areas.Study.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        [BindProperty]
        public int SelectedPart { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (SelectedPart < 1 || SelectedPart > 7)
            {
                ModelState.AddModelError(string.Empty, "Please select a valid Part to practice.");
                return Page();
            }

            var memberKey = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var memberName = User.FindFirst("MemberName")?.Value;

            if (string.IsNullOrEmpty(memberKey))
            {
                return RedirectToPage("/Account/Login", new { area = "" });
            }

            var memberKeyGuid = Guid.Parse(memberKey);

            try
            {
                // 2. Check for the latest unfinished study session for this Part and User
                var (existingTestKey, existingResultKey) = await IndexAccessData.CheckForUnfinishedStudySession(memberKeyGuid, SelectedPart);

                if (existingTestKey.HasValue && existingResultKey.HasValue)
                {
                    // *** SỬA LỖI ĐIỀU HƯỚNG TẠI ĐÂY ***
                    // If an unfinished session exists, redirect the user to the Study page with all required parameters.
                    return RedirectToPage("/Study", new
                    {
                        area = "Study",
                        testKey = existingTestKey.ToString(),
                        resultKey = existingResultKey.ToString(),
                        selectedPart = SelectedPart
                    });
                }

                // --- If no unfinished session is found, create a new one ---

                // 3. Get configuration for the selected Part
                var (config, distribution) = await IndexAccessData.GetPartConfiguration(SelectedPart);
                if (config == null || distribution == null)
                {
                    ModelState.AddModelError(string.Empty, "Configuration for the selected Part could not be found in the database.");
                    return Page();
                }

                // 4. Create a new 'Test' record
                var newTestKey = Guid.NewGuid();
                var testName = $"TOEIC STUDY Part {SelectedPart} - {memberKey}";
                var description = $"{memberName} - {DateTime.Now}";
                await IndexAccessData.InsertTest(newTestKey, testName, description, config.TotalQuestion, config.Duration, memberKeyGuid, memberName);

                // 5. Create a new 'ResultOfUserForTest' record for the study session (EndTime is NULL)
                var newResultKey = Guid.NewGuid();
                var startTime = DateTime.Now;
                await IndexAccessData.InsertStudyResult(newResultKey, newTestKey, memberKeyGuid, memberName, startTime);

                // 6. Generate the test content
                await IndexAccessData.GenerateTestContentForPart(newTestKey, newResultKey, SelectedPart, config, distribution);

                // *** SỬA LỖI ĐIỀU HƯỚNG TẠI ĐÂY ***
                // 7. Redirect the user to the new Study page with the new keys and part info
                return RedirectToPage("/Study", new
                {
                    area = "Study",
                    testKey = newTestKey.ToString(),
                    resultKey = newResultKey.ToString(),
                    selectedPart = SelectedPart
                });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"An error occurred while creating the study session: {ex.Message}");
                return Page();
            }
        }
    }
}