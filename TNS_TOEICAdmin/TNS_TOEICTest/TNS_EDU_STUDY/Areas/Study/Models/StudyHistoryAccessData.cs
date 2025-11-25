using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;

namespace TNS_EDU_TEST.Areas.Test.Models 
{
    public class StudyHistoryItem
    {
        public string TestName { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string MemberName { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? TimeSpent { get; set; } 
        public int? PracticeScore { get; set; } 
        public string TestKey { get; set; }
        public string ResultKey { get; set; }
        public int Part { get; set; } 
    }

    public static class StudyHistoryAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;


        public static List<StudyHistoryItem> LoadStudyHistory(string memberKey, int partSelect)
        {
            var items = new List<StudyHistoryItem>();

            // 1. Định nghĩa 2 mẫu tên bài thi
            string standardPattern = $"TOEIC STUDY Part {partSelect}%";
            string adaptivePattern = $"Adaptive Practice Part {partSelect}%";

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // 2. Sửa câu SQL dùng OR để lấy cả 2 loại
                string sql = @"
                    SELECT 
                        t.TestName, 
                        t.CreatedOn, 
                        r.MemberName, 
                        r.StartTime, 
                        r.EndTime, 
                        r.Time, 
                        r.TestScore, 
                        t.TestKey,
                        r.ResultKey
                    FROM Test t
                    INNER JOIN ResultOfUserForTest r ON t.TestKey = r.TestKey
                    WHERE 
                        r.MemberKey = @MemberKey 
                        AND (t.TestName LIKE @StandardPattern OR t.TestName LIKE @AdaptivePattern) -- Lấy cả 2 loại
                        AND r.Status = 1 -- Chỉ lấy những bài đã nộp
                    ORDER BY r.StartTime DESC";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                    cmd.Parameters.AddWithValue("@StandardPattern", standardPattern);
                    cmd.Parameters.AddWithValue("@AdaptivePattern", adaptivePattern);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // 3. Xử lý làm sạch tên hiển thị
                            string rawName = reader["TestName"].ToString();
                            string displayName = rawName;

                            // Nếu là bài Study thường: "TOEIC STUDY Part 1 - {Guid}" -> Cắt theo " - "
                            if (rawName.Contains(" - "))
                            {
                                displayName = rawName.Split(" - ")[0].Trim();
                            }
                            // Nếu là bài Adaptive: "Adaptive Practice Part 1 [{Guid}]" -> Cắt theo " ["
                            else if (rawName.Contains(" ["))
                            {
                                displayName = rawName.Split(" [")[0].Trim();
                            }

                            items.Add(new StudyHistoryItem
                            {
                                TestName = displayName, // Tên đã được làm sạch
                                CreatedOn = reader["CreatedOn"] as DateTime?,
                                MemberName = reader["MemberName"].ToString(),
                                StartTime = reader["StartTime"] as DateTime?,
                                EndTime = reader["EndTime"] as DateTime?,
                                TimeSpent = reader["Time"] as int?,
                                PracticeScore = reader["TestScore"] as int?,
                                TestKey = reader["TestKey"].ToString(),
                                ResultKey = reader["ResultKey"].ToString(),
                                Part = partSelect
                            });
                        }
                    }
                }
            }

            return items;
        }
    }
}
