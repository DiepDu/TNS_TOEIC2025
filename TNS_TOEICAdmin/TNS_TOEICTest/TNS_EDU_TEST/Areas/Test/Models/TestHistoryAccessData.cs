using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace TNS_EDU_TEST.Areas.Test.Models
{
    public class TestHistoryAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
        public static List<TestHistoryItem> LoadTestHistory(string memberKey)
        {
            List<TestHistoryItem> items = new List<TestHistoryItem>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                string sql = @"
            SELECT 
                t.TestName, 
                t.CreatedOn, 
                r.MemberName, 
                r.StartTime, 
                r.EndTime, 
                r.Time,  -- Lấy giá trị Time (số phút)
                r.ListeningScore, 
                r.ReadingScore, 
                r.TestScore,
                t.TestKey,
                r.ResultKey
            FROM Test t
            INNER JOIN ResultOfUserForTest r ON t.TestKey = r.TestKey
            WHERE r.MemberKey = @MemberKey
            ORDER BY t.CreatedOn DESC";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@MemberKey", memberKey);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new TestHistoryItem
                            {
                                TestName = reader["TestName"].ToString(),
                                CreatedOn = reader["CreatedOn"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["CreatedOn"]),
                                MemberName = reader["MemberName"].ToString(),
                                StartTime = reader["StartTime"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["StartTime"]),
                                EndTime = reader["EndTime"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["EndTime"]),
                                Time = reader["Time"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["Time"]),
                                ListeningScore = reader["ListeningScore"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["ListeningScore"]),
                                ReadingScore = reader["ReadingScore"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["ReadingScore"]),
                                TestScore = reader["TestScore"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["TestScore"]),
                                TestKey = reader["TestKey"].ToString(),
                                ResultKey = reader["ResultKey"].ToString()
                            });
                        }
                    }
                }
            }

            return items;
        }
        public class TestHistoryItem
        {
            public string TestName { get; set; }
            public DateTime? CreatedOn { get; set; }
            public string MemberName { get; set; }
            public DateTime? StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public int? Time { get; set; }  // Số phút làm bài
            public int? ListeningScore { get; set; }
            public int? ReadingScore { get; set; }
            public int? TestScore { get; set; }
            public string TestKey { get; set; }
            public string ResultKey { get; set; }
        }
    }
}
