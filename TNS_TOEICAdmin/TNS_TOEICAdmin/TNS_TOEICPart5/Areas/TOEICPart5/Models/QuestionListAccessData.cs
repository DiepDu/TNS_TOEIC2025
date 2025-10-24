using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TNS_TOEICPart5.Areas.TOEICPart5.Models
{
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

    public static class QuestionListDataAccess
    {
        // ✅ Helper methods
        private static string BuildWhereClause(bool hasDateFilter)
        {
            string where = hasDateFilter
                ? "WHERE (CreatedOn >= @FromDate AND CreatedOn <= @ToDate) AND QuestionText LIKE @Search AND (QuestionKey IS NOT NULL) "
                : "WHERE QuestionText LIKE @Search AND (QuestionKey IS NOT NULL) ";
            return where;
        }

        private static string BuildStatusFilter(string statusFilter)
        {
            if (string.IsNullOrEmpty(statusFilter)) return "";

            if (statusFilter == "Using")
                return " AND Publish = 1 AND RecordStatus != 99 ";
            else if (statusFilter == "Unpublished")
                return " AND Publish = 0 ";
            else if (statusFilter == "Deleted")
                return " AND RecordStatus = 99 ";

            return "";
        }

        // ✅ Method without date - Returns data + totalCount
        public static JsonResult GetList(string Search, int Level, int PageSize, int PageNumber, string StatusFilter)
        {
            string zMessage = "";

            // Count query
            string zCountSQL = @"SELECT COUNT(*) FROM [dbo].[TEC_Part5_Question] " + BuildWhereClause(false);
            if (Level > 0)
                zCountSQL += " AND SkillLevel = @Level ";
            zCountSQL += BuildStatusFilter(StatusFilter);

            // Data query with pagination
            string zDataSQL = @"SELECT QuestionKey, QuestionText, QuestionVoice, SkillLevel, AmountAccess, CorrectRate, Anomaly, Publish, RecordStatus 
                       FROM [dbo].[TEC_Part5_Question] " + BuildWhereClause(false);

            if (Level > 0)
                zDataSQL += " AND SkillLevel = @Level ";

            zDataSQL += BuildStatusFilter(StatusFilter);
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

                    // Get total count
                    using (SqlCommand zCountCommand = new SqlCommand(zCountSQL, zConnect))
                    {
                        zCountCommand.CommandType = CommandType.Text;
                        zCountCommand.Parameters.Add("@Level", SqlDbType.Int).Value = Level;
                        zCountCommand.Parameters.Add("@Search", SqlDbType.NVarChar).Value = "%" + Search + "%";
                        totalCount = (int)zCountCommand.ExecuteScalar();
                    }

                    // Get data
                    using (SqlCommand zCommand = new SqlCommand(zDataSQL, zConnect))
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
                QuestionVoice = row["QuestionVoice"].ToString() ?? "",
                SkillLevel = row["SkillLevel"] != DBNull.Value ? Convert.ToInt32(row["SkillLevel"]) : 0,
                AmountAccess = row["AmountAccess"] != DBNull.Value ? Convert.ToInt32(row["AmountAccess"]) : 0,
                Publish = row["Publish"] != DBNull.Value ? Convert.ToBoolean(row["Publish"]) : false,
                RecordStatus = row["RecordStatus"] != DBNull.Value ? Convert.ToInt32(row["RecordStatus"]) : 0,
                CorrectRate = row["CorrectRate"] != DBNull.Value ? Convert.ToDouble(row["CorrectRate"]) : (double?)null,
                Anomaly = row["Anomaly"] != DBNull.Value ? Convert.ToInt32(row["Anomaly"]) : (int?)null
            }).ToList();

            return new JsonResult(new { data = zDataList, totalCount = totalCount });
        }

        // ✅ Method with date - Returns data + totalCount
        public static JsonResult GetList(string Search, int Level, DateTime FromDate, DateTime ToDate, int PageSize, int PageNumber, string StatusFilter)
        {
            string zMessage = "";

            // Count query
            string zCountSQL = @"SELECT COUNT(*) FROM [dbo].[TEC_Part5_Question] " + BuildWhereClause(true);
            if (Level > 0)
                zCountSQL += " AND SkillLevel = @Level ";
            zCountSQL += BuildStatusFilter(StatusFilter);

            // Data query with pagination
            string zDataSQL = @"SELECT QuestionKey, QuestionText, QuestionVoice, SkillLevel, AmountAccess, CorrectRate, Anomaly, Publish, RecordStatus 
                       FROM [dbo].[TEC_Part5_Question] " + BuildWhereClause(true);

            if (Level > 0)
                zDataSQL += " AND SkillLevel = @Level ";

            zDataSQL += BuildStatusFilter(StatusFilter);
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

                    // Get total count
                    using (SqlCommand zCountCommand = new SqlCommand(zCountSQL, zConnect))
                    {
                        zCountCommand.CommandType = CommandType.Text;
                        zCountCommand.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = new DateTime(FromDate.Year, FromDate.Month, FromDate.Day, 0, 0, 1);
                        zCountCommand.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = new DateTime(ToDate.Year, ToDate.Month, ToDate.Day, 23, 59, 59);
                        zCountCommand.Parameters.Add("@Search", SqlDbType.NVarChar).Value = "%" + Search + "%";
                        zCountCommand.Parameters.Add("@Level", SqlDbType.Int).Value = Level;
                        totalCount = (int)zCountCommand.ExecuteScalar();
                    }

                    // Get data
                    using (SqlCommand zCommand = new SqlCommand(zDataSQL, zConnect))
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
                QuestionVoice = row["QuestionVoice"].ToString() ?? "",
                SkillLevel = row["SkillLevel"] != DBNull.Value ? Convert.ToInt32(row["SkillLevel"]) : 0,
                AmountAccess = row["AmountAccess"] != DBNull.Value ? Convert.ToInt32(row["AmountAccess"]) : 0,
                Publish = row["Publish"] != DBNull.Value ? Convert.ToBoolean(row["Publish"]) : false,
                RecordStatus = row["RecordStatus"] != DBNull.Value ? Convert.ToInt32(row["RecordStatus"]) : 0,
                CorrectRate = row["CorrectRate"] != DBNull.Value ? Convert.ToDouble(row["CorrectRate"]) : (double?)null,
                Anomaly = row["Anomaly"] != DBNull.Value ? Convert.ToInt32(row["Anomaly"]) : (int?)null
            }).ToList();

            return new JsonResult(new { data = zDataList, totalCount = totalCount });
        }
    }
}