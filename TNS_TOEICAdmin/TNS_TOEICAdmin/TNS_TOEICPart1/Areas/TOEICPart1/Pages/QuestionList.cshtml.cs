using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Data;
using System.Data.SqlClient;
using static System.Net.Mime.MediaTypeNames;
using System.Linq;

namespace TNS_TOEICPart1.Areas.TOEICPart1.Pages
{
    [IgnoreAntiforgeryToken]
    public class QuestionListModel : PageModel
    {
        #region [ Security ]
        public TNS.Auth.UserLogin_Info UserLogin;

        private void CheckAuth()
        {
            UserLogin = new TNS.Auth.UserLogin_Info(User);
            UserLogin.GetRole("TOEIC_Part1");
            // For Testing
            UserLogin.Role.IsRead = true;
            UserLogin.Role.IsCreate = true;
            UserLogin.Role.IsUpdate = true;
            UserLogin.Role.IsDelete = true;
        }
        #endregion

        public IActionResult OnGet()
        {
            CheckAuth();
            if (UserLogin.Role.IsRead)
            {
                return Page();
            }
            else
            {
                return LocalRedirect("~/Warning?id=403");
            }
        }

        public IActionResult OnPostLoadData([FromBody] ItemRequest request)
        {
            CheckAuth();
            JsonResult zResult = new JsonResult("");
            if (UserLogin.Role.IsRead)
            {
                DateTime zFromDate, zToDate;
                if (request.FromDate.Trim().Length > 0 && request.ToDate.Trim().Length > 0)
                {
                    zFromDate = DateTime.Parse(request.FromDate);
                    zToDate = DateTime.Parse(request.ToDate);
                    zResult = QuestionListDataAccess.GetList(request.Search, request.Level, zFromDate, zToDate, request.PageSize, request.PageNumber);
                }
                else
                {
                    zResult = QuestionListDataAccess.GetList(request.Search, request.Level, request.PageSize, request.PageNumber);
                }
            }
            else
            {
                zResult = new JsonResult(new { Status = "ERROR", Result = "ACCESS DENIED" });
            }
            return zResult;
        }

        public class ItemRequest
        {
            public string FromDate { get; set; }
            public string ToDate { get; set; }
            public string Search { get; set; }
            public int Level { get; set; }
            public int PageSize { get; set; }
            public int PageNumber { get; set; }
        }

        public static class QuestionListDataAccess
        {
            public static JsonResult GetList(string Search, int Level, int PageSize, int PageNumber)
            {
                string zMessage = "";
                string zSQL = @"SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, SkillLevel, AmountAccess 
                       FROM [dbo].[TEC_Part1_Question] 
                       WHERE RecordStatus != 99 AND QuestionText LIKE @Search 
                       AND Publish = 1 
                       AND(QuestionKey IS NOT NULL) "; 
                if (Level > 0)
                    zSQL += " AND SkillLevel = @Level ";
                zSQL += " ORDER BY CreatedOn DESC ";
                    zSQL += "      OFFSET @PageSize *(@PageNumber - 1) ROWS FETCH NEXT @PageSize ROWS ONLY OPTION(RECOMPILE)";
        
        DataTable zTable = new DataTable();
                string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
                try
                {
                    using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                    {
                        zConnect.Open();
                        using (SqlCommand zCommand = new SqlCommand(zSQL, zConnect))
                        {
                            zCommand.CommandType = CommandType.Text;
                            zCommand.Parameters.Add("@Level", SqlDbType.Int).Value = Level;
                            zCommand.Parameters.Add("@Search", SqlDbType.NVarChar).Value = "%" + Search + "%";
                            zCommand.Parameters.Add("@PageSize", SqlDbType.Int).Value = PageSize;
                            zCommand.Parameters.Add("@PageNumber", SqlDbType.Int).Value = PageNumber;
                            using (SqlDataAdapter zAdapter = new SqlDataAdapter(zCommand))
                            {
                                zAdapter.Fill(zTable);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    zMessage = ex.ToString();
                }
                var zDataList = zTable.AsEnumerable().Select(row => new
                {
                    QuestionKey = row["QuestionKey"].ToString(),
                    QuestionText = row["QuestionText"].ToString() ?? "",
                    QuestionImage = row["QuestionImage"].ToString() ?? "",
                    QuestionVoice = row["QuestionVoice"].ToString() ?? "",
                    SkillLevel = row["SkillLevel"] != DBNull.Value ? Convert.ToInt32(row["SkillLevel"]) : 0,
                    AmountAccess = row["AmountAccess"] != DBNull.Value ? Convert.ToInt32(row["AmountAccess"]) : 0
                }).ToList();

                return new JsonResult(zDataList);
            }

            public static JsonResult GetList(string Search, int Level, DateTime FromDate, DateTime ToDate, int PageSize, int PageNumber)
            {
                string zMessage = "";
                string zSQL = @"SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, SkillLevel, AmountAccess 
                       FROM [dbo].[TEC_Part1_Question] 
                       WHERE RecordStatus != 99 AND (CreatedOn >= @FromDate AND CreatedOn <= @ToDate) 
                       AND QuestionText LIKE @Search 
                       AND Publish = 1 
                       AND(QuestionKey IS NOT NULL) "; 
                if (Level > 0)
                    zSQL += " AND SkillLevel = @Level ";
                zSQL += " ORDER BY CreatedOn DESC ";
                 zSQL += "         OFFSET @PageSize *(@PageNumber - 1) ROWS FETCH NEXT @PageSize ROWS ONLY OPTION(RECOMPILE)";
        
        DataTable zTable = new DataTable();
                string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
                try
                {
                    using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                    {
                        zConnect.Open();
                        using (SqlCommand zCommand = new SqlCommand(zSQL, zConnect))
                        {
                            zCommand.CommandType = CommandType.Text;
                            zCommand.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = new DateTime(FromDate.Year, FromDate.Month, FromDate.Day, 0, 0, 1);
                            zCommand.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = new DateTime(ToDate.Year, ToDate.Month, ToDate.Day, 23, 59, 59);
                            zCommand.Parameters.Add("@Search", SqlDbType.NVarChar).Value = "%" + Search + "%";
                            zCommand.Parameters.Add("@Level", SqlDbType.Int).Value = Level;
                            zCommand.Parameters.Add("@PageSize", SqlDbType.Int).Value = PageSize;
                            zCommand.Parameters.Add("@PageNumber", SqlDbType.Int).Value = PageNumber;
                            using (SqlDataAdapter zAdapter = new SqlDataAdapter(zCommand))
                            {
                                zAdapter.Fill(zTable);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    zMessage = ex.ToString();
                }
                var zDataList = zTable.AsEnumerable().Select(row => new
                {
                    QuestionKey = row["QuestionKey"].ToString(),
                    QuestionText = row["QuestionText"].ToString() ?? "",
                    QuestionImage = row["QuestionImage"].ToString() ?? "",
                    QuestionVoice = row["QuestionVoice"].ToString() ?? "",
                    SkillLevel = row["SkillLevel"] != DBNull.Value ? Convert.ToInt32(row["SkillLevel"]) : 0,
                    AmountAccess = row["AmountAccess"] != DBNull.Value ? Convert.ToInt32(row["AmountAccess"]) : 0
                }).ToList();

                return new JsonResult(zDataList);
            }
        }
    }
}