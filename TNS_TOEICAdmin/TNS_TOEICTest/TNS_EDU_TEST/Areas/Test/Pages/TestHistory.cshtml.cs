using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TNS_EDU_TEST.Areas.Test.Models;
using static TNS_EDU_TEST.Areas.Test.Models.TestHistoryAccessData;
using Microsoft.AspNetCore.Authorization;

namespace TNS_EDU_TEST.Areas.Test.Pages
{
    [IgnoreAntiforgeryToken]
    [Authorize]
    public class TestHistoryModel : PageModel
    {
        public List<TestHistoryItem> TestHistoryItems { get; set; } = new List<TestHistoryItem>();
        private readonly IConfiguration _configuration;

        public TestHistoryModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }


        public void OnGet()
        {

            string memberKey = User.Claims.FirstOrDefault(c => c.Type == "MemberKey" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(memberKey))
            {

                return;
            }


            TestHistoryItems = TestHistoryAccessData.LoadTestHistory(memberKey);
        }



    }


}