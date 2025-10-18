using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Reflection;
using System.Runtime.InteropServices;
using TNS_Auth;

namespace TNS_TOEICAdmin.Models
{
    public class MembersAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        public static async Task<(List<Member> Members, int TotalRecords)> GetMembersAsync(int page = 1, int pageSize = 10, string search = null, string activate = null, string testStatus = null)
        {
            var members = new List<Member>();
            int totalRecords = 0;

            string baseSql = @"
                FROM [EDU_Member] m
                WHERE 1=1";

            if (!string.IsNullOrEmpty(search))
                baseSql += " AND (m.MemberID LIKE @Search OR m.MemberName LIKE @Search)";
            if (!string.IsNullOrEmpty(activate))
                baseSql += " AND m.Activate = @Activate";

            // Count total records
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string countSql = "SELECT COUNT(*) " + baseSql;
                using (var countCmd = new SqlCommand(countSql, conn))
                {
                    if (!string.IsNullOrEmpty(search))
                        countCmd.Parameters.AddWithValue("@Search", "%" + search + "%");
                    if (!string.IsNullOrEmpty(activate))
                        countCmd.Parameters.AddWithValue("@Activate", activate == "1");

                    totalRecords = (int)await countCmd.ExecuteScalarAsync();
                }

                // Get paginated data
                string sql = @"
                    SELECT 
                        m.MemberKey, m.MemberID, m.MemberName, m.Activate, 
                        m.ToeicScoreExam, m.LastLoginDate, m.IrtAbility, m.ScoreTarget
                    " + baseSql + @"
                    ORDER BY m.CreatedOn DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    if (!string.IsNullOrEmpty(search))
                        cmd.Parameters.AddWithValue("@Search", "%" + search + "%");
                    if (!string.IsNullOrEmpty(activate))
                        cmd.Parameters.AddWithValue("@Activate", activate == "1");
                    cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            members.Add(new Member
                            {
                                MemberKey = reader.GetGuid("MemberKey"),
                                MemberID = reader.IsDBNull("MemberID") ? "N/A" : reader.GetString("MemberID"),
                                MemberName = reader.IsDBNull("MemberName") ? "N/A" : reader.GetString("MemberName"),
                                Activate = reader.GetBoolean("Activate"),
                                ToeicScoreExam = reader.IsDBNull("ToeicScoreExam") ? null : reader.GetInt32("ToeicScoreExam"),
                                LastLoginDate = reader.IsDBNull("LastLoginDate") ? null : reader.GetDateTime("LastLoginDate").ToString("yyyy-MM-dd HH:mm"),
                                IrtAbility = reader.IsDBNull("IrtAbility") ? null : (float?)reader.GetDouble("IrtAbility"),
                                ScoreTarget = reader.IsDBNull("ScoreTarget") ? null : reader.GetInt32("ScoreTarget")
                            });
                        }
                    }
                }
            }
            return (members, totalRecords);
        }
        public static async Task<Member> GetMemberDetailsAsync(Guid memberKey)
        {
            Member member = null;
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = @"
                    SELECT 
                        m.*, d.DepartmentName 
                    FROM [EDU_Member] m
                    LEFT JOIN [HRM_Department] d ON m.DepartmentKey = d.DepartmentKey
                    WHERE m.MemberKey = @MemberKey";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            member = new Member
                            {
                                MemberKey = reader.GetGuid("MemberKey"),
                                MemberID = reader.GetString("MemberID"),
                                MemberName = reader.GetString("MemberName"),
                                Gender = reader.GetInt32("Gender"),
                                YearOld = reader.GetInt32("YearOld"),
                                Activate = reader.GetBoolean("Activate"),
                                ToeicScoreExam = reader.IsDBNull("ToeicScoreExam") ? null : reader.GetInt32("ToeicScoreExam"),
                                LastLoginDate = reader.IsDBNull("LastLoginDate") ? null : reader.GetDateTime("LastLoginDate").ToString("yyyy-MM-dd HH:mm:ss"),
                                DepartmentName = reader.IsDBNull("DepartmentName") ? null : reader.GetString("DepartmentName"),
                                DepartmentKey = reader.IsDBNull("DepartmentKey") ? null : reader.GetGuid("DepartmentKey"),
                                IrtAbility = reader.IsDBNull("IrtAbility") ? null : (float?)reader.GetDouble("IrtAbility"),
                                ScoreTarget = reader.IsDBNull("ScoreTarget") ? null : reader.GetInt32("ScoreTarget"),
                                PracticeScore_Part1 = reader.IsDBNull("PracticeScore_Part1") ? null : reader.GetInt32("PracticeScore_Part1"),
                                PracticeScore_Part2 = reader.IsDBNull("PracticeScore_Part2") ? null : reader.GetInt32("PracticeScore_Part2"),
                                PracticeScore_Part3 = reader.IsDBNull("PracticeScore_Part3") ? null : reader.GetInt32("PracticeScore_Part3"),
                                PracticeScore_Part4 = reader.IsDBNull("PracticeScore_Part4") ? null : reader.GetInt32("PracticeScore_Part4"),
                                PracticeScore_Part5 = reader.IsDBNull("PracticeScore_Part5") ? null : reader.GetInt32("PracticeScore_Part5"),
                                PracticeScore_Part6 = reader.IsDBNull("PracticeScore_Part6") ? null : reader.GetInt32("PracticeScore_Part6"),
                                PracticeScore_Part7 = reader.IsDBNull("PracticeScore_Part7") ? null : reader.GetInt32("PracticeScore_Part7")
                            };
                        }
                    }
                }
            }
            return member;
        }
        public static async Task AddMemberAsync(Member member)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                // Check for duplicate MemberID
                string checkSql = "SELECT COUNT(*) FROM [EDU_Member] WHERE MemberID = @MemberID";
                using (var checkCmd = new SqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("@MemberID", member.MemberID);
                    if ((int)await checkCmd.ExecuteScalarAsync() > 0)
                    {
                        throw new InvalidOperationException("MemberID already exists.");
                    }
                }

                string sql = @"
                    INSERT INTO [EDU_Member] 
                        (MemberKey, MemberID, MemberName, Password, Gender, YearOld, DepartmentKey, Activate, 
                        ToeicScoreExam, ScoreTarget, IrtAbility, CreatedBy, CreatedOn)
                    VALUES 
                        (@MemberKey, @MemberID, @MemberName, @Password, @Gender, @YearOld, @DepartmentKey, @Activate, 
                        @ToeicScoreExam, @ScoreTarget, @IrtAbility, @CreatedBy, GETDATE())";
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
                    cmd.Parameters.AddWithValue("@ToeicScoreExam", (object)member.ToeicScoreExam ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ScoreTarget", (object)member.ScoreTarget ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IrtAbility", (object)member.IrtAbility ?? DBNull.Value);
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
                string checkSql = "SELECT COUNT(*) FROM [EDU_Member] WHERE MemberID = @MemberID AND MemberKey != @MemberKey";
                using (var checkCmd = new SqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("@MemberID", member.MemberID);
                    checkCmd.Parameters.AddWithValue("@MemberKey", member.MemberKey);
                    if ((int)await checkCmd.ExecuteScalarAsync() > 0)
                    {
                        throw new InvalidOperationException("MemberID already exists for another member.");
                    }
                }
                string sql = @"
                    UPDATE [EDU_Member]
                    SET MemberID = @MemberID, 
                        MemberName = @MemberName, 
                        Password = CASE WHEN @Password IS NOT NULL THEN @Password ELSE Password END, 
                        Gender = @Gender, 
                        YearOld = @YearOld, 
                        DepartmentKey = @DepartmentKey, 
                        Activate = @Activate,
                        ToeicScoreExam = @ToeicScoreExam,
                        ScoreTarget = @ScoreTarget,
                        IrtAbility = @IrtAbility,
                        ModifiedBy = @ModifiedBy,
                        ModifiedOn = GETDATE()
                    WHERE MemberKey = @MemberKey";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@MemberKey", member.MemberKey);
                    cmd.Parameters.AddWithValue("@MemberID", member.MemberID);
                    cmd.Parameters.AddWithValue("@MemberName", member.MemberName);
                    cmd.Parameters.AddWithValue("@Password", (object)(!string.IsNullOrEmpty(member.Password) ? MyCryptographyMembers.HashPassMember(member.Password) : null) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Gender", member.Gender);
                    cmd.Parameters.AddWithValue("@YearOld", member.YearOld);
                    cmd.Parameters.AddWithValue("@DepartmentKey", (object)member.DepartmentKey ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Activate", member.Activate);
                    cmd.Parameters.AddWithValue("@ToeicScoreExam", (object)member.ToeicScoreExam ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ScoreTarget", (object)member.ScoreTarget ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IrtAbility", (object)member.IrtAbility ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ModifiedBy", member.ModifiedBy);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
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

        //public static async Task AddMemberAsync(Member member)
        //{
        //    using (var conn = new SqlConnection(_connectionString))
        //    {
        //        await conn.OpenAsync();

        //        // Kiểm tra trùng MemberID
        //        string checkSql = "SELECT COUNT(*) FROM [EDU_Member] WHERE MemberID = @MemberID";
        //        using (var checkCmd = new SqlCommand(checkSql, conn))
        //        {
        //            checkCmd.Parameters.AddWithValue("@MemberID", member.MemberID);
        //            int count = (int)await checkCmd.ExecuteScalarAsync();
        //            if (count > 0)
        //            {
        //                throw new InvalidOperationException("MemberID already exists.");
        //            }
        //        }

        //        // Nếu không trùng, tiến hành thêm member
        //        string sql = @"
        //    INSERT INTO [EDU_Member] (MemberKey, MemberID, MemberName, Password, Gender, YearOld, DepartmentKey, Activate, ToeicScoreStudy, ToeicScoreExam, CreatedBy, CreatedOn)
        //    VALUES (@MemberKey, @MemberID, @MemberName, @Password, @Gender, @YearOld, @DepartmentKey, @Activate, @ToeicScoreStudy, @ToeicScoreExam, @CreatedBy, GETDATE())";
        //        using (var cmd = new SqlCommand(sql, conn))
        //        {
        //            cmd.Parameters.AddWithValue("@MemberKey", member.MemberKey);
        //            cmd.Parameters.AddWithValue("@MemberID", member.MemberID);
        //            cmd.Parameters.AddWithValue("@MemberName", member.MemberName);
        //            cmd.Parameters.AddWithValue("@Password", MyCryptographyMembers.HashPassMember(member.Password));
        //            cmd.Parameters.AddWithValue("@Gender", member.Gender);
        //            cmd.Parameters.AddWithValue("@YearOld", member.YearOld);
        //            cmd.Parameters.AddWithValue("@DepartmentKey", (object)member.DepartmentKey ?? DBNull.Value);
        //            cmd.Parameters.AddWithValue("@Activate", member.Activate);
        //            cmd.Parameters.AddWithValue("@ToeicScoreStudy", (object)member.ToeicScoreStudy ?? DBNull.Value);
        //            cmd.Parameters.AddWithValue("@ToeicScoreExam", (object)member.ToeicScoreExam ?? DBNull.Value);
        //            cmd.Parameters.AddWithValue("@CreatedBy", member.CreatedBy);
        //            await cmd.ExecuteNonQueryAsync();
        //        }
        //    }
        //}

        //public static async Task UpdateMemberAsync(Member member)
        //{
        //    using (var conn = new SqlConnection(_connectionString))
        //    {
        //        await conn.OpenAsync();

        //        // Kiểm tra trùng MemberID, nhưng loại trừ bản ghi hiện tại (dựa trên MemberKey)
        //        string checkSql = "SELECT COUNT(*) FROM [EDU_Member] WHERE MemberID = @MemberID AND MemberKey != @MemberKey";
        //        using (var checkCmd = new SqlCommand(checkSql, conn))
        //        {
        //            checkCmd.Parameters.AddWithValue("@MemberID", member.MemberID);
        //            checkCmd.Parameters.AddWithValue("@MemberKey", member.MemberKey);
        //            int count = (int)await checkCmd.ExecuteScalarAsync();
        //            if (count > 0)
        //            {
        //                throw new InvalidOperationException("MemberID already exists.");
        //            }
        //        }

        //        // Nếu không trùng, tiến hành sửa member
        //        string sql = @"
        //    UPDATE [EDU_Member]
        //    SET MemberID = @MemberID, 
        //        MemberName = @MemberName, 
        //        Password = CASE WHEN @Password IS NOT NULL THEN @Password ELSE Password END, 
        //        Gender = @Gender, 
        //        YearOld = @YearOld, 
        //        DepartmentKey = @DepartmentKey, 
        //        Activate = @Activate,
        //        ToeicScoreStudy = @ToeicScoreStudy,
        //        ToeicScoreExam = @ToeicScoreExam,
        //        ModifiedBy = @ModifiedBy,
        //        ModifiedOn = GETDATE()
        //    WHERE MemberKey = @MemberKey";
        //        using (var cmd = new SqlCommand(sql, conn))
        //        {
        //            cmd.Parameters.AddWithValue("@MemberKey", member.MemberKey);
        //            cmd.Parameters.AddWithValue("@MemberID", member.MemberID);
        //            cmd.Parameters.AddWithValue("@MemberName", member.MemberName);
        //            cmd.Parameters.AddWithValue("@Password", (object)(member.Password != null ? MyCryptographyMembers.HashPassMember(member.Password) : null) ?? DBNull.Value);
        //            cmd.Parameters.AddWithValue("@Gender", member.Gender);
        //            cmd.Parameters.AddWithValue("@YearOld", member.YearOld);
        //            cmd.Parameters.AddWithValue("@DepartmentKey", (object)member.DepartmentKey ?? DBNull.Value);
        //            cmd.Parameters.AddWithValue("@Activate", member.Activate);
        //            cmd.Parameters.AddWithValue("@ToeicScoreStudy", (object)member.ToeicScoreStudy ?? DBNull.Value);
        //            cmd.Parameters.AddWithValue("@ToeicScoreExam", (object)member.ToeicScoreExam ?? DBNull.Value);
        //            cmd.Parameters.AddWithValue("@ModifiedBy", member.ModifiedBy);
        //            await cmd.ExecuteNonQueryAsync();
        //        }
        //    }
        //}

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
      

public static async Task<List<FullTestHistory>> GetFullTestHistoryAsync(Guid memberKey)
        {
            var history = new List<FullTestHistory>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                // THAY ĐỔI SQL: Thêm r.Status vào SELECT và cập nhật WHERE
                string sql = @"
            SELECT 
                t.TestName, 
                r.StartTime, 
                r.EndTime, 
                r.Time, 
                r.ListeningScore, 
                r.ReadingScore, 
                r.TestScore,
                r.ResultKey,
                t.TestKey,
                r.Status
            FROM Test t
            INNER JOIN ResultOfUserForTest r ON t.TestKey = r.TestKey
            WHERE r.MemberKey = @MemberKey 
              AND (t.TestName LIKE 'TOEIC Full Test%' OR t.TestName LIKE 'Full Test%') 
              AND r.Status IN (1, 2, 99) -- Lấy cả bài đã hoàn thành, đã sửa và đã hủy
            ORDER BY r.StartTime ASC";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            history.Add(new FullTestHistory
                            {
                                TestName = reader.GetString("TestName"),
                                StartTime = reader.GetDateTime("StartTime"),
                                EndTime = reader.IsDBNull("EndTime") ? (DateTime?)null : reader.GetDateTime("EndTime"),
                                TimeSpent = reader.IsDBNull("Time") ? (int?)null : reader.GetInt32("Time"),
                                ListeningScore = reader.IsDBNull("ListeningScore") ? (int?)null : reader.GetInt32("ListeningScore"),
                                ReadingScore = reader.IsDBNull("ReadingScore") ? (int?)null : reader.GetInt32("ReadingScore"),
                                TestScore = reader.IsDBNull("TestScore") ? (int?)null : reader.GetInt32("TestScore"),
                                ResultKey = reader.GetGuid("ResultKey"),
                                TestKey = reader.GetGuid("TestKey"),
                                Status = reader.GetInt32("Status") // THÊM DÒNG NÀY
                            });
                        }
                    }
                }
            }
            return history;
        }

        public static async Task<List<PracticeTestHistory>> GetPracticeHistoryAsync(Guid memberKey, string part = null)
        {
            var history = new List<PracticeTestHistory>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                // THAY ĐỔI SQL: Thêm r.Status vào SELECT và cập nhật WHERE
                string sql = @"
            SELECT 
                t.TestName, 
                r.StartTime, 
                r.EndTime, 
                r.Time,
                r.TestScore,
                r.ResultKey,
                t.TestKey,
                r.Status
            FROM Test t
            INNER JOIN ResultOfUserForTest r ON t.TestKey = r.TestKey
            WHERE 
                r.MemberKey = @MemberKey 
                AND t.TestName LIKE '%Part%'
                AND r.Status IN (1, 2, 99)"; // Lấy cả bài đã hoàn thành, đã sửa và đã hủy

                if (!string.IsNullOrEmpty(part))
                {
                    sql += " AND t.TestName LIKE @PartPattern";
                }
                sql += " ORDER BY r.StartTime ASC";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                    if (!string.IsNullOrEmpty(part))
                    {
                        cmd.Parameters.AddWithValue("@PartPattern", $"%Part {part}%");
                    }
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            history.Add(new PracticeTestHistory
                            {
                                TestName = reader.GetString("TestName"),
                                StartTime = reader.GetDateTime("StartTime"),
                                EndTime = reader.IsDBNull("EndTime") ? (DateTime?)null : reader.GetDateTime("EndTime"),
                                TimeSpent = reader.IsDBNull("Time") ? (int?)null : reader.GetInt32("Time"),
                                PracticeScore = reader.IsDBNull("TestScore") ? (int?)null : reader.GetInt32("TestScore"),
                                ResultKey = reader.GetGuid("ResultKey"),
                                TestKey = reader.GetGuid("TestKey"),
                                Status = reader.GetInt32("Status") // THÊM DÒNG NÀY
                            });
                        }
                    }
                }
            }
            return history;
        }
    }

    public class FullTestHistory
    {
        public string TestName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? TimeSpent { get; set; }
        public int? ListeningScore { get; set; }
        public int? ReadingScore { get; set; }
        public int? TestScore { get; set; }
        public Guid ResultKey { get; set; }
        public Guid TestKey { get; set; }
        public int Status { get; set; }
    }

    public class PracticeTestHistory
    {
        public string TestName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? TimeSpent { get; set; }
        public int? PracticeScore { get; set; } // Percentage
        public Guid ResultKey { get; set; }
        public Guid TestKey { get; set; }
        public int Status { get; set; }
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
        public int? ToeicScoreExam { get; set; }
        public int? ScoreTarget { get; set; }
        public float? IrtAbility { get; set; }
        public int? PracticeScore_Part1 { get; set; }
        public int? PracticeScore_Part2 { get; set; }
        public int? PracticeScore_Part3 { get; set; }
        public int? PracticeScore_Part4 { get; set; }
        public int? PracticeScore_Part5 { get; set; }
        public int? PracticeScore_Part6 { get; set; }
        public int? PracticeScore_Part7 { get; set; }
        public string LastLoginDate { get; set; }
        public Guid CreatedBy { get; set; }
        public Guid ModifiedBy { get; set; }
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