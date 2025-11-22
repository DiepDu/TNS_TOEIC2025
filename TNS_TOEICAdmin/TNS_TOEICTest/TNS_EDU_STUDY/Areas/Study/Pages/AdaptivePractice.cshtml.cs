using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using TNS_EDU_STUDY.Areas.Study.Models;

namespace TNS_EDU_STUDY.Areas.Study.Pages
{
    public class AdaptivePracticeModel : PageModel
    {
        public Dictionary<int, bool> PartsStatus { get; set; } = new Dictionary<int, bool>();
        public string MemberKey { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            MemberKey = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(MemberKey))
            {
                return RedirectToPage("/Account/Login");
            }

            PartsStatus = await AdaptivePracticeAccessData.GetAllPartsStatusAsync(MemberKey);
            return Page();
        }

        /// <summary>
        /// AJAX Handler: Get analysis data for a specific part
        /// </summary>
        public async Task<IActionResult> OnGetPartAnalysisAsync(int part)
        {
            MemberKey = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(MemberKey))
            {
                return new JsonResult(new { success = false, message = "Unauthorized" });
            }

            if (part < 1 || part > 7)
            {
                return new JsonResult(new { success = false, message = "Invalid part number" });
            }

            try
            {
                var analysis = await AdaptivePracticeAccessData.GetPartAnalysisAsync(MemberKey, part);

                if (analysis == null)
                {
                    return new JsonResult(new
                    {
                        success = true,
                        hasData = false,
                        message = "No analysis data available for this part yet."
                    });
                }

                return new JsonResult(new
                {
                    success = true,
                    hasData = true,
                    data = new
                    {
                        part = analysis.Part,
                        speedScore = analysis.SpeedScore,
                        decisivenessScore = analysis.DecisivenessScore,
                        accuracyScore = analysis.AccuracyScore,
                        avgTimeSpent = analysis.AvgTimeSpent,
                        abilityTemporary = analysis.AbilityTemporary,
                        lastAnalyzed = analysis.LastAnalyzed.ToString("yyyy-MM-dd"),
                        advice = analysis.Advice ?? "",
                        weakTopics = new
                        {
                            summary = analysis.WeakTopics?.Summary ?? "",
                            grammar = analysis.WeakTopics?.Grammar ?? new List<string>(),
                            vocab = analysis.WeakTopics?.Vocab ?? new List<string>(),
                            errors = analysis.WeakTopics?.Errors ?? new List<string>(),
                            categories = analysis.WeakTopics?.Categories ?? new List<string>(),
                            actionableRecommendations = analysis.WeakTopics?.ActionableRecommendations ?? new List<string>()
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetPartAnalysisAsync: {ex.Message}");
                return new JsonResult(new
                {
                    success = false,
                    message = $"Error loading analysis: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// ✅ NEW: Handler for "Start Adaptive Practice" button
        /// </summary>
        public async Task<IActionResult> OnPostStartAdaptivePracticeAsync(int part)
        {
            try
            {
                var memberKey = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var memberName = User.FindFirst("MemberName")?.Value;

                if (string.IsNullOrEmpty(memberKey))
                {
                    return new JsonResult(new { success = false, message = "Unauthorized" });
                }

                var memberKeyGuid = Guid.Parse(memberKey);

                Console.WriteLine($"[StartAdaptivePractice] Member: {memberKey}, Part: {part}");

                // Create adaptive test
                var (testKey, resultKey) = await StartAdaptivePracticeAccessData.CreateAdaptiveTestAsync(
                    memberKeyGuid, memberName, part);

                Console.WriteLine($"[StartAdaptivePractice] SUCCESS - TestKey: {testKey}, ResultKey: {resultKey}");

                // Return redirect URL
                return new JsonResult(new
                {
                    success = true,
                    redirectUrl = Url.Page("/Study", new
                    {
                        area = "Study",
                        testKey = testKey.ToString(),
                        resultKey = resultKey.ToString(),
                        selectedPart = part
                    })
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StartAdaptivePractice ERROR]: {ex.Message}");
                return new JsonResult(new
                {
                    success = false,
                    message = $"Failed to create adaptive test: {ex.Message}"
                });
            }
        }
    }
}