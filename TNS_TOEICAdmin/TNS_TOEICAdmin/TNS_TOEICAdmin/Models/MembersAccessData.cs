using Microsoft.Data.SqlClient;
using TNS_Auth;

namespace TNS_TOEICAdmin.Models
{
    public class MembersAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        public static async Task<List<Member>> GetMembersAsync(int page = 1, int pageSize = 10, string search = null, string activate = null, string testStatus = null)
        {
            var members = new List<Member>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = @"
                    SELECT 
                        m.MemberKey, m.MemberID, m.MemberName, m.Gender, m.YearOld, m.Activate, 
                        m.ToeicScoreStudy, m.ToeicScoreExam, m.LastLoginDate, d.DepartmentName, m.DepartmentKey,
                        COUNT(CASE WHEN r.Status != 99 THEN r.TestKey END) AS TotalTests,
                        AVG(CASE WHEN r.Status != 99 THEN CAST(r.TestScore AS FLOAT) END) AS AverageTestScore
                    FROM [EDU_Member] m
                    LEFT JOIN [HRM_Department] d ON m.DepartmentKey = d.DepartmentKey
                    LEFT JOIN [ResultOfUserForTest] r ON m.MemberKey = r.MemberKey
                    WHERE 1=1";

                if (!string.IsNullOrEmpty(search))
                    sql += " AND (m.MemberID LIKE @Search OR m.MemberName LIKE @Search)";
                if (!string.IsNullOrEmpty(activate))
                    sql += " AND m.Activate = @Activate";
                if (!string.IsNullOrEmpty(testStatus))
                {
                    if (testStatus == "inprogress")
                        sql += " AND EXISTS (SELECT 1 FROM ResultOfUserForTest r2 WHERE r2.MemberKey = m.MemberKey AND r2.TestScore IS NULL AND r2.Status = 0)";
                    else if (testStatus == "completed")
                        sql += " AND EXISTS (SELECT 1 FROM ResultOfUserForTest r2 WHERE r2.MemberKey = m.MemberKey AND r2.TestScore IS NOT NULL AND r2.Status IN (1, 2))";
                    else if (testStatus == "notstarted")
                        sql += " AND NOT EXISTS (SELECT 1 FROM ResultOfUserForTest r2 WHERE r2.MemberKey = m.MemberKey)";
                }

                sql += @"
                    GROUP BY 
                        m.MemberKey, m.MemberID, m.MemberName, m.Gender, m.YearOld, m.Activate, 
                        m.ToeicScoreStudy, m.ToeicScoreExam, m.LastLoginDate, d.DepartmentName, m.DepartmentKey
                    ORDER BY m.MemberName
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    if (!string.IsNullOrEmpty(search))
                        cmd.Parameters.AddWithValue("@Search", "%" + search + "%");
                    if (!string.IsNullOrEmpty(activate))
                        cmd.Parameters.AddWithValue("@Activate", activate == "1");
                    cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    try
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                members.Add(new Member
                                {
                                    MemberKey = reader.GetGuid(0),
                                    MemberID = reader.IsDBNull(1) ? "N/A" : reader.GetString(1),
                                    MemberName = reader.IsDBNull(2) ? "N/A" : reader.GetString(2),
                                    Gender = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                    YearOld = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                                    Activate = reader.IsDBNull(5) ? false : reader.GetBoolean(5),
                                    ToeicScoreStudy = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                                    ToeicScoreExam = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                                    LastLoginDate = reader.IsDBNull(8) ? null : reader.GetDateTime(8).ToString("yyyy-MM-dd HH:mm:ss"),
                                    DepartmentName = reader.IsDBNull(9) ? null : reader.GetString(9),
                                    DepartmentKey = reader.IsDBNull(10) ? null : reader.GetGuid(10),
                                    TotalTests = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                                    AverageTestScore = reader.IsDBNull(12) ? null : (double?)reader.GetDouble(12)
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading members: {ex.Message}");
                        throw;
                    }
                }
            }
            return members;
        }
        public static async Task<int> GetTotalMembersAsync(string search = null, string activate = null, string testStatus = null)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = @"
            SELECT COUNT(DISTINCT m.MemberKey)
            FROM [EDU_Member] m
            LEFT JOIN [ResultOfUserForTest] r ON m.MemberKey = r.MemberKey
            WHERE 1=1";

                if (!string.IsNullOrEmpty(search))
                    sql += " AND (m.MemberID LIKE @Search OR m.MemberName LIKE @Search)";
                if (!string.IsNullOrEmpty(activate))
                    sql += " AND m.Activate = @Activate";
                if (!string.IsNullOrEmpty(testStatus))
                {
                    if (testStatus == "inprogress")
                        sql += " AND EXISTS (SELECT 1 FROM ResultOfUserForTest r2 WHERE r2.MemberKey = m.MemberKey AND r2.TestScore IS NULL AND r2.Status = 0)";
                    else if (testStatus == "completed")
                        sql += " AND EXISTS (SELECT 1 FROM ResultOfUserForTest r2 WHERE r2.MemberKey = m.MemberKey AND r2.TestScore IS NOT NULL AND r2.Status IN (1, 2))";
                    else if (testStatus == "notstarted")
                        sql += " AND NOT EXISTS (SELECT 1 FROM ResultOfUserForTest r2 WHERE r2.MemberKey = m.MemberKey)";
                }

                using (var cmd = new SqlCommand(sql, conn))
                {
                    if (!string.IsNullOrEmpty(search))
                        cmd.Parameters.AddWithValue("@Search", "%" + search + "%");
                    if (!string.IsNullOrEmpty(activate))
                        cmd.Parameters.AddWithValue("@Activate", activate == "1");
                    try
                    {
                        return (int)await cmd.ExecuteScalarAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in GetTotalMembersAsync: {ex.Message}");
                        throw;
                    }
                }
            }
        }
        public static async Task<List<MemberDepartment>> GetDepartmentsAsync()
        {
            var departments = new List<MemberDepartment>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = "SELECT DepartmentKey, DepartmentName FROM [HRM_Department]";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            departments.Add(new MemberDepartment
                            {
                                DepartmentKey = reader.GetGuid(0),
                                DepartmentName = reader.GetString(1)
                            });
                        }
                    }
                }
            }
            return departments;
        }

        public static async Task<List<TestDetail>> GetTestDetailsAsync(Guid memberKey)
        {
            var tests = new List<TestDetail>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = @"
                    SELECT r.TestKey, r.ResultKey, r.TestScore, r.StartTime, r.EndTime, r.Status, r.Time, t.TestName
                    FROM [ResultOfUserForTest] r
                    LEFT JOIN [Test] t ON r.TestKey = t.TestKey
                    WHERE r.MemberKey = @MemberKey
                    ORDER BY r.StartTime DESC";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            tests.Add(new TestDetail
                            {
                                TestKey = reader.GetGuid(0),
                                ResultKey = reader.GetGuid(1),
                                TestScore = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                                StartTime = reader.IsDBNull(3) ? null : reader.GetDateTime(3).ToString("yyyy-MM-dd HH:mm:ss"),
                                EndTime = reader.IsDBNull(4) ? null : reader.GetDateTime(4).ToString("yyyy-MM-dd HH:mm:ss"),
                                Status = reader.GetInt32(5),
                                Time = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                                TestName = reader.IsDBNull(7) ? null : reader.GetString(7)
                            });
                        }
                    }
                }
            }
            return tests;
        }

        public static async Task UpdateTestScoreAsync(Guid resultKey, int testScore)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = @"
                    UPDATE [ResultOfUserForTest]
                    SET TestScore = @TestScore, Status = 2
                    WHERE ResultKey = @ResultKey";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    cmd.Parameters.AddWithValue("@TestScore", testScore);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                        throw new InvalidOperationException("No test result found with the provided ResultKey.");
                }
            }
        }

        public static async Task CancelTestAsync(Guid resultKey)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = @"
                    UPDATE [ResultOfUserForTest]
                    SET Status = 99
                    WHERE ResultKey = @ResultKey";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                        throw new InvalidOperationException("No test result found with the provided ResultKey.");
                }
            }
        }

        public static async Task AddMemberAsync(Member member)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Kiểm tra trùng MemberID
                string checkSql = "SELECT COUNT(*) FROM [EDU_Member] WHERE MemberID = @MemberID";
                using (var checkCmd = new SqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("@MemberID", member.MemberID);
                    int count = (int)await checkCmd.ExecuteScalarAsync();
                    if (count > 0)
                    {
                        throw new InvalidOperationException("MemberID already exists.");
                    }
                }

                // Nếu không trùng, tiến hành thêm member
                string sql = @"
            INSERT INTO [EDU_Member] (MemberKey, MemberID, MemberName, Password, Gender, YearOld, DepartmentKey, Activate, ToeicScoreStudy, ToeicScoreExam, CreatedBy, CreatedOn)
            VALUES (@MemberKey, @MemberID, @MemberName, @Password, @Gender, @YearOld, @DepartmentKey, @Activate, @ToeicScoreStudy, @ToeicScoreExam, @CreatedBy, GETDATE())";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@MemberKey", member.MemberKey);
                    cmd.Parameters.AddWithValue("@MemberID", member.MemberID);
                    cmd.Parameters.AddWithValue("@MemberName", member.MemberName);
                    cmd.Parameters.AddWithValue("@Password", MyCryptographyMembers.HashPassMember(member.Password));
                    cmd.Parameters.AddWithValue("@Gender", member.Gender);
                    cmd.Parameters.AddWithValue("@YearOld", member.YearOld);
                    cmd.Parameters.AddWithValue("@DepartmentKey", (object)member.DepartmentKey ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Activate", member.Activate);
                    cmd.Parameters.AddWithValue("@ToeicScoreStudy", (object)member.ToeicScoreStudy ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ToeicScoreExam", (object)member.ToeicScoreExam ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CreatedBy", member.CreatedBy);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public static async Task UpdateMemberAsync(Member member)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Kiểm tra trùng MemberID, nhưng loại trừ bản ghi hiện tại (dựa trên MemberKey)
                string checkSql = "SELECT COUNT(*) FROM [EDU_Member] WHERE MemberID = @MemberID AND MemberKey != @MemberKey";
                using (var checkCmd = new SqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("@MemberID", member.MemberID);
                    checkCmd.Parameters.AddWithValue("@MemberKey", member.MemberKey);
                    int count = (int)await checkCmd.ExecuteScalarAsync();
                    if (count > 0)
                    {
                        throw new InvalidOperationException("MemberID already exists.");
                    }
                }

                // Nếu không trùng, tiến hành sửa member
                string sql = @"
            UPDATE [EDU_Member]
            SET MemberID = @MemberID, 
                MemberName = @MemberName, 
                Password = CASE WHEN @Password IS NOT NULL THEN @Password ELSE Password END, 
                Gender = @Gender, 
                YearOld = @YearOld, 
                DepartmentKey = @DepartmentKey, 
                Activate = @Activate,
                ToeicScoreStudy = @ToeicScoreStudy,
                ToeicScoreExam = @ToeicScoreExam,
                ModifiedBy = @ModifiedBy,
                ModifiedOn = GETDATE()
            WHERE MemberKey = @MemberKey";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@MemberKey", member.MemberKey);
                    cmd.Parameters.AddWithValue("@MemberID", member.MemberID);
                    cmd.Parameters.AddWithValue("@MemberName", member.MemberName);
                    cmd.Parameters.AddWithValue("@Password", (object)(member.Password != null ? MyCryptographyMembers.HashPassMember(member.Password) : null) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Gender", member.Gender);
                    cmd.Parameters.AddWithValue("@YearOld", member.YearOld);
                    cmd.Parameters.AddWithValue("@DepartmentKey", (object)member.DepartmentKey ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Activate", member.Activate);
                    cmd.Parameters.AddWithValue("@ToeicScoreStudy", (object)member.ToeicScoreStudy ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ToeicScoreExam", (object)member.ToeicScoreExam ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ModifiedBy", member.ModifiedBy);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public static async Task DeleteMemberAsync(Guid memberKey)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = "DELETE FROM [ResultOfUserForTest] WHERE MemberKey = @MemberKey; DELETE FROM [EDU_Member] WHERE MemberKey = @MemberKey";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
        public static async Task<List<TestScoreHistory>> GetTestScoreHistoryAsync(Guid memberKey)
        {
            var history = new List<TestScoreHistory>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = @"
            SELECT 
                r.TestScore, r.ListeningScore, r.ReadingScore, r.StartTime, t.TestName
            FROM [ResultOfUserForTest] r
            LEFT JOIN [Test] t ON r.TestKey = t.TestKey
            WHERE r.MemberKey = @MemberKey AND r.Status != 99 
                AND (r.TestScore IS NOT NULL OR r.ListeningScore IS NOT NULL OR r.ReadingScore IS NOT NULL)
            ORDER BY r.StartTime ASC";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            history.Add(new TestScoreHistory
                            {
                                TestScore = reader.IsDBNull(0) ? null : reader.GetInt32(0),
                                ListeningScore = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                                ReadingScore = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                                StartTime = reader.GetDateTime(3),
                                TestName = reader.IsDBNull(4) ? "N/A" : reader.GetString(4)
                            });
                        }
                    }
                }
            }
            return history;
        }
    }
   
    public class TestScoreHistory
    {
        public int? TestScore { get; set; }
        public int? ListeningScore { get; set; }
        public int? ReadingScore { get; set; }
        public DateTime StartTime { get; set; }
        public string TestName { get; set; }
    }
    public class Member
    {
        public Guid MemberKey { get; set; }
        public string MemberID { get; set; }
        public string MemberName { get; set; }
        public string Password { get; set; }
        public int Gender { get; set; }
        public int YearOld { get; set; }
        public Guid? DepartmentKey { get; set; }
        public string DepartmentName { get; set; }
        public bool Activate { get; set; }
        public int? ToeicScoreStudy { get; set; }
        public int? ToeicScoreExam { get; set; }
        public string LastLoginDate { get; set; }
        public Guid CreatedBy { get; set; }
        public Guid ModifiedBy { get; set; }
        public int TotalTests { get; set; }
        public double? AverageTestScore { get; set; }
    }

    public class MemberDepartment
    {
        public Guid DepartmentKey { get; set; }
        public string DepartmentName { get; set; }
    }

    public class TestDetail
    {
        public Guid TestKey { get; set; }
        public Guid ResultKey { get; set; }
        public int? TestScore { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public int Status { get; set; }
        public int? Time { get; set; }
        public string TestName { get; set; }
    }
}