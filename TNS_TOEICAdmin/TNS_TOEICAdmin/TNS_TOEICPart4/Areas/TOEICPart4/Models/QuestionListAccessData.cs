using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace TNS_TOEICPart4.Areas.TOEICPart4.Models
{
    public static class QuestionListDataAccess
    {
        // ✅ Phương thức không có ngày - CÓ TOTAL COUNT
        public static JsonResult GetList(string Search, int Level, int PageSize, int PageNumber, string StatusFilter)
        {
            string zMessage = "";

            // ✅ Query để đếm tổng số records
            string zCountSQL = @"SELECT COUNT(*) 
                       FROM [dbo].[TEC_Part4_Question] 
                       WHERE QuestionText LIKE @Search AND Parent IS NULL
                       AND (QuestionKey IS NOT NULL) ";

            // ✅ Query để lấy data phân trang
            string zDataSQL = @"SELECT QuestionKey, QuestionText, QuestionVoice, SkillLevel, AmountAccess, Publish, RecordStatus 
                       FROM [dbo].[TEC_Part4_Question] 
                       WHERE QuestionText LIKE @Search AND Parent IS NULL
                       AND (QuestionKey IS NOT NULL) ";

            // Apply StatusFilter cho cả 2 queries
            string statusFilterClause = "";
            if (!string.IsNullOrEmpty(StatusFilter))
            {
                if (StatusFilter == "Using")
                {
                    statusFilterClause = " AND Publish = 1 AND RecordStatus != 99 ";
                }
                else if (StatusFilter == "Unpublished")
                {
                    statusFilterClause = " AND Publish = 0 ";
                }
                else if (StatusFilter == "Deleted")
                {
                    statusFilterClause = " AND RecordStatus = 99 ";
                }
            }

            zCountSQL += statusFilterClause;
            zDataSQL += statusFilterClause;

            // Apply Level filter
            if (Level > 0)
            {
                zCountSQL += " AND SkillLevel = @Level ";
                zDataSQL += " AND SkillLevel = @Level ";
            }

            zDataSQL += " ORDER BY CreatedOn DESC ";
            zDataSQL += " OFFSET @PageSize * (@PageNumber - 1) ROWS FETCH NEXT @PageSize ROWS ONLY OPTION(RECOMPILE)";

            DataTable zTable = new DataTable();
            int totalCount = 0;
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

            try
            {
                using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                {
                    zConnect.Open();

                    // ✅ Đếm tổng số records
                    using (SqlCommand zCountCommand = new SqlCommand(zCountSQL, zConnect))
                    {
                        zCountCommand.CommandType = CommandType.Text;
                        zCountCommand.Parameters.Add("@Level", SqlDbType.Int).Value = Level;
                        zCountCommand.Parameters.Add("@Search", SqlDbType.NVarChar).Value = "%" + Search + "%";
                        totalCount = (int)zCountCommand.ExecuteScalar();
                    }

                    // ✅ Lấy data phân trang
                    using (SqlCommand zDataCommand = new SqlCommand(zDataSQL, zConnect))
                    {
                        zDataCommand.CommandType = CommandType.Text;
                        zDataCommand.Parameters.Add("@Level", SqlDbType.Int).Value = Level;
                        zDataCommand.Parameters.Add("@Search", SqlDbType.NVarChar).Value = "%" + Search + "%";
                        zDataCommand.Parameters.Add("@PageSize", SqlDbType.Int).Value = PageSize;
                        zDataCommand.Parameters.Add("@PageNumber", SqlDbType.Int).Value = PageNumber;
                        using (SqlDataAdapter zAdapter = new SqlDataAdapter(zDataCommand))
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
                QuestionVoice = row["QuestionVoice"].ToString() ?? "",
                SkillLevel = row["SkillLevel"] != DBNull.Value ? Convert.ToInt32(row["SkillLevel"]) : 0,
                AmountAccess = row["AmountAccess"] != DBNull.Value ? Convert.ToInt32(row["AmountAccess"]) : 0,
                Publish = row["Publish"] != DBNull.Value ? Convert.ToBoolean(row["Publish"]) : false,
                RecordStatus = row["RecordStatus"] != DBNull.Value ? Convert.ToInt32(row["RecordStatus"]) : 0
              
            }).ToList();

            // ✅ Trả về object với data và totalCount
            return new JsonResult(new
            {
                data = zDataList,
                totalCount = totalCount
            });
        }

        // ✅ Phương thức có ngày - CÓ TOTAL COUNT
        public static JsonResult GetList(string Search, int Level, DateTime FromDate, DateTime ToDate, int PageSize, int PageNumber, string StatusFilter)
        {
            string zMessage = "";

            // ✅ Query để đếm tổng số records
            string zCountSQL = @"SELECT COUNT(*) 
                       FROM [dbo].[TEC_Part4_Question] 
                       WHERE (CreatedOn >= @FromDate AND CreatedOn <= @ToDate) 
                       AND QuestionText LIKE @Search AND Parent IS NULL
                       AND (QuestionKey IS NOT NULL) ";

            // ✅ Query để lấy data phân trang
            string zDataSQL = @"SELECT QuestionKey, QuestionText, QuestionVoice, SkillLevel, AmountAccess, Publish, RecordStatus 
                       FROM [dbo].[TEC_Part4_Question] 
                       WHERE (CreatedOn >= @FromDate AND CreatedOn <= @ToDate) 
                       AND QuestionText LIKE @Search AND Parent IS NULL
                       AND (QuestionKey IS NOT NULL) ";

            // Apply StatusFilter
            string statusFilterClause = "";
            if (!string.IsNullOrEmpty(StatusFilter))
            {
                if (StatusFilter == "Using")
                {
                    statusFilterClause = " AND Publish = 1 AND RecordStatus != 99 ";
                }
                else if (StatusFilter == "Unpublished")
                {
                    statusFilterClause = " AND Publish = 0 ";
                }
                else if (StatusFilter == "Deleted")
                {
                    statusFilterClause = " AND RecordStatus = 99 ";
                }
            }

            zCountSQL += statusFilterClause;
            zDataSQL += statusFilterClause;

            // Apply Level filter
            if (Level > 0)
            {
                zCountSQL += " AND SkillLevel = @Level ";
                zDataSQL += " AND SkillLevel = @Level ";
            }

            zDataSQL += " ORDER BY CreatedOn DESC ";
            zDataSQL += " OFFSET @PageSize * (@PageNumber - 1) ROWS FETCH NEXT @PageSize ROWS ONLY OPTION(RECOMPILE)";

            DataTable zTable = new DataTable();
            int totalCount = 0;
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

            try
            {
                using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                {
                    zConnect.Open();

                    // ✅ Đếm tổng số records
                    using (SqlCommand zCountCommand = new SqlCommand(zCountSQL, zConnect))
                    {
                        zCountCommand.CommandType = CommandType.Text;
                        zCountCommand.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = new DateTime(FromDate.Year, FromDate.Month, FromDate.Day, 0, 0, 1);
                        zCountCommand.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = new DateTime(ToDate.Year, ToDate.Month, ToDate.Day, 23, 59, 59);
                        zCountCommand.Parameters.Add("@Search", SqlDbType.NVarChar).Value = "%" + Search + "%";
                        zCountCommand.Parameters.Add("@Level", SqlDbType.Int).Value = Level;
                        totalCount = (int)zCountCommand.ExecuteScalar();
                    }

                    // ✅ Lấy data phân trang
                    using (SqlCommand zDataCommand = new SqlCommand(zDataSQL, zConnect))
                    {
                        zDataCommand.CommandType = CommandType.Text;
                        zDataCommand.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = new DateTime(FromDate.Year, FromDate.Month, FromDate.Day, 0, 0, 1);
                        zDataCommand.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = new DateTime(ToDate.Year, ToDate.Month, ToDate.Day, 23, 59, 59);
                        zDataCommand.Parameters.Add("@Search", SqlDbType.NVarChar).Value = "%" + Search + "%";
                        zDataCommand.Parameters.Add("@Level", SqlDbType.Int).Value = Level;
                        zDataCommand.Parameters.Add("@PageSize", SqlDbType.Int).Value = PageSize;
                        zDataCommand.Parameters.Add("@PageNumber", SqlDbType.Int).Value = PageNumber;
                        using (SqlDataAdapter zAdapter = new SqlDataAdapter(zDataCommand))
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
                QuestionVoice = row["QuestionVoice"].ToString() ?? "",
                SkillLevel = row["SkillLevel"] != DBNull.Value ? Convert.ToInt32(row["SkillLevel"]) : 0,
                AmountAccess = row["AmountAccess"] != DBNull.Value ? Convert.ToInt32(row["AmountAccess"]) : 0,
                Publish = row["Publish"] != DBNull.Value ? Convert.ToBoolean(row["Publish"]) : false,
                RecordStatus = row["RecordStatus"] != DBNull.Value ? Convert.ToInt32(row["RecordStatus"]) : 0
             
            }).ToList();

            // ✅ Trả về object với data và totalCount
            return new JsonResult(new
            {
                data = zDataList,
                totalCount = totalCount
            });
        }
    }

    public class ItemRequest
    {
        public string FromDate { get; set; }
        public string ToDate { get; set; }
        public string Search { get; set; }
        public int Level { get; set; }
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public string? StatusFilter { get; set; }
    }
}