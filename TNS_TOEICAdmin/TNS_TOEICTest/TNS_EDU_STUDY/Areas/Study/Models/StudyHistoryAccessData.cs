using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;

namespace TNS_EDU_TEST.Areas.Test.Models // Bạn có thể đổi namespace này cho phù hợp
{
    // Lớp định nghĩa cấu trúc dữ liệu cho một dòng lịch sử
    public class StudyHistoryItem
    {
        public string TestName { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string MemberName { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? TimeSpent { get; set; } // Số phút làm bài
        public int? PracticeScore { get; set; } // Điểm luyện tập (%)
        public string TestKey { get; set; }
        public string ResultKey { get; set; }
        public int Part { get; set; } // Thêm Part để có thể dùng cho link Review
    }

    public static class StudyHistoryAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        // Hàm chính để tải lịch sử, giờ đây có thêm tham số partSelect
        public static List<StudyHistoryItem> LoadStudyHistory(string memberKey, int partSelect)
        {
            var items = new List<StudyHistoryItem>();

            // Xây dựng chuỗi mẫu để tìm kiếm theo tên TestName
            // Ví dụ: "TOEIC STUDY Part 1%"
            string testNamePattern = $"TOEIC STUDY Part {partSelect}%";

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // Câu lệnh SQL đã được sửa đổi để lọc theo TestName Pattern
                string sql = @"
                    SELECT 
                        t.TestName, 
                        t.CreatedOn, 
                        r.MemberName, 
                        r.StartTime, 
                        r.EndTime, 
                        r.Time,  -- Đây là TimeSpent (số phút)
                        r.TestScore, -- Đây là PracticeScore (%)
                        t.TestKey,
                        r.ResultKey
                    FROM Test t
                    INNER JOIN ResultOfUserForTest r ON t.TestKey = r.TestKey
                    WHERE 
                        r.MemberKey = @MemberKey 
                        AND t.TestName LIKE @TestNamePattern
                        AND r.Status = 1 -- Chỉ lấy những bài đã nộp
                    ORDER BY r.StartTime DESC"; // Sắp xếp theo thời gian bắt đầu gần nhất

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                    cmd.Parameters.AddWithValue("@TestNamePattern", testNamePattern);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new StudyHistoryItem
                            {
                                // Xử lý tên bài thi để hiển thị đẹp hơn (bỏ phần GUID)
                                TestName = reader["TestName"].ToString().Split(" - ")[0].Trim(),
                                CreatedOn = reader["CreatedOn"] as DateTime?,
                                MemberName = reader["MemberName"].ToString(),
                                StartTime = reader["StartTime"] as DateTime?,
                                EndTime = reader["EndTime"] as DateTime?,
                                TimeSpent = reader["Time"] as int?,
                                PracticeScore = reader["TestScore"] as int?,
                                TestKey = reader["TestKey"].ToString(),
                                ResultKey = reader["ResultKey"].ToString(),
                                Part = partSelect // Lưu lại part đã chọn
                            });
                        }
                    }
                }
            }

            return items;
        }
    }
}
