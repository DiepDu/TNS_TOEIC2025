using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TNS_TOEICPart6.Areas.TOEICPart6.Models
{
    public static class AnswerListDataAccess
    {
        public static JsonResult GetList(string questionKey)
        {
            string zMessage = "";
            string zSQL = @"SELECT AnswerKey, AnswerText, AnswerCorrect 
                           FROM [dbo].[TEC_Part6_Answer] 
                           WHERE RecordStatus != 99 AND QuestionKey = @QuestionKey 
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
                        zCommand.Parameters.Add("@QuestionKey", SqlDbType.NVarChar).Value = questionKey;
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
                AnswerKey = row["AnswerKey"].ToString(),
                AnswerText = row["AnswerText"].ToString() ?? "",
                AnswerCorrect = Convert.ToBoolean(row["AnswerCorrect"])
            }).ToList();

            return new JsonResult(zDataList);
        }
    }
}
