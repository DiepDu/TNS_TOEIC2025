using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

public class QuestionSubListAccessData
{
    public static JsonResult GetList(string QuestionKey)
    {
        // 💡 **Phòng tránh lỗi:** Nếu QuestionKey là null hoặc rỗng, trả về danh sách trống
        if (string.IsNullOrEmpty(QuestionKey))
        {
            return new JsonResult(new List<object>());
        }

        string zMessage = "";

        // ✅ UPDATED: Added IRT columns
        string zSQL = @"SELECT QuestionKey, QuestionText, QuestionImage, SkillLevel, 
                               AmountAccess, CorrectRate, Anomaly, Ranking,
                               IrtDifficulty, IrtDiscrimination, IrtGuessing, 
                               Quality, ConfidenceLevel, LastAnalyzed
                        FROM [dbo].[TEC_Part6_Question] 
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
                    zCommand.Parameters.Add("@QuestionKey", SqlDbType.UniqueIdentifier).Value = Guid.Parse(QuestionKey);

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
            return new JsonResult(new { Status = "ERROR", Message = zMessage });
        }

        if (!string.IsNullOrEmpty(zMessage))
        {
            return new JsonResult(new { Status = "ERROR", Message = zMessage });
        }

        // ✅ UPDATED: Added IRT fields to output
        var zDataList = zTable.AsEnumerable().Select(row => new Dictionary<string, object>
        {
            { "QuestionKey", row["QuestionKey"] },
            { "QuestionText", row["QuestionText"] },
            { "QuestionImage", row["QuestionImage"] },
            { "SkillLevel", row["SkillLevel"] },
            { "AmountAccess", row["AmountAccess"] },
            { "CorrectRate", row["CorrectRate"] == DBNull.Value ? null : Convert.ToDouble(row["CorrectRate"]) },
            { "Anomaly", row["Anomaly"] == DBNull.Value ? null : Convert.ToInt32(row["Anomaly"]) },
            { "Ranking", row["Ranking"] },
            // ✅ NEW: IRT Parameters
            { "IrtDifficulty", row["IrtDifficulty"] == DBNull.Value ? null : Convert.ToDouble(row["IrtDifficulty"]) },
            { "IrtDiscrimination", row["IrtDiscrimination"] == DBNull.Value ? null : Convert.ToDouble(row["IrtDiscrimination"]) },
            { "IrtGuessing", row["IrtGuessing"] == DBNull.Value ? null : Convert.ToDouble(row["IrtGuessing"]) },
            { "Quality", row["Quality"] == DBNull.Value ? "" : row["Quality"].ToString() },
            { "ConfidenceLevel", row["ConfidenceLevel"] == DBNull.Value ? "" : row["ConfidenceLevel"].ToString() },
            { "LastAnalyzed", row["LastAnalyzed"] == DBNull.Value ? "" : Convert.ToDateTime(row["LastAnalyzed"]).ToString("yyyy-MM-dd HH:mm") }
        }).ToList();

        return new JsonResult(zDataList);
    }
}