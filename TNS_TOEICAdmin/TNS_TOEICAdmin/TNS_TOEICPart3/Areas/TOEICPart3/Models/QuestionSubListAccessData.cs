using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace TNS_TOEICPart3.Areas.TOEICPart3.Models
{
    public class QuestionSubListAccessData
    {
        // ✅ GIỮ LẠI CorrectRate và Anomaly cho sub-questions
        public static JsonResult GetList(string QuestionKey)
        {
            string zMessage = "";

            string zSQL = @"SELECT QuestionKey, QuestionText, QuestionImage, SkillLevel, 
                                   AmountAccess, CorrectRate, Anomaly, Ranking
                           FROM [dbo].[TEC_Part3_Question] 
                           WHERE RecordStatus != 99 AND Parent = @QuestionKey
                           ORDER BY Ranking";

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
                        zCommand.Parameters.Add("@QuestionKey", SqlDbType.NVarChar).Value = QuestionKey;
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

            // ✅ GIỮ LẠI CorrectRate và Anomaly
            var zDataList = zTable.AsEnumerable().Select(row => new Dictionary<string, object>
            {
                { "QuestionKey", row["QuestionKey"] },
                { "QuestionText", row["QuestionText"] },
                { "QuestionImage", row["QuestionImage"] },
                { "SkillLevel", row["SkillLevel"] },
                { "AmountAccess", row["AmountAccess"] },
                { "CorrectRate", row["CorrectRate"] == DBNull.Value ? null : Convert.ToDouble(row["CorrectRate"]) },
                { "Anomaly", row["Anomaly"] == DBNull.Value ? null : Convert.ToInt32(row["Anomaly"]) },
                { "Ranking", row["Ranking"] }
            }).ToList();

            return new JsonResult(zDataList);
        }
    }
}