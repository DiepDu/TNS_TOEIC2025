using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



public class QuestionSubListAccessData
    {
        public static JsonResult GetList(string QuestionKey)
        {
            // 💡 **Phòng tránh lỗi:** Nếu QuestionKey là null hoặc rỗng, trả về danh sách trống ngay lập tức.
            if (string.IsNullOrEmpty(QuestionKey))
            {
                return new JsonResult(new List<object>()); // Trả về mảng rỗng
            }

            string zMessage = "";
            string zSQL = @"SELECT QuestionKey, QuestionText,QuestionImage, SkillLevel, AmountAccess, CorrectRate, Anomaly, Ranking
                        FROM [dbo].[TEC_Part6_Question] 
                        WHERE RecordStatus != 99 AND Parent = @QuestionKey
                        ORDER BY Ranking ";
            DataTable zTable = new DataTable();
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            try
            {
                SqlConnection zConnect = new SqlConnection(zConnectionString);
                zConnect.Open();
                SqlCommand zCommand = new SqlCommand(zSQL, zConnect);
                zCommand.CommandType = CommandType.Text;

                // Đảm bảo kiểu dữ liệu là UniqueIdentifier
                zCommand.Parameters.Add("@QuestionKey", SqlDbType.UniqueIdentifier).Value = Guid.Parse(QuestionKey);

                SqlDataAdapter zAdapter = new SqlDataAdapter(zCommand);
                zAdapter.Fill(zTable);
                zCommand.Dispose();
                zConnect.Close();
            }
            catch (Exception ex)
            {
                zMessage = ex.ToString();
                // Bạn nên ghi log lỗi này thay vì chỉ gán vào chuỗi
            }

            if (!string.IsNullOrEmpty(zMessage))
            {
                // Trả về một đối tượng lỗi để client có thể xử lý
                return new JsonResult(new { Status = "ERROR", Message = zMessage });
            }

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


