using Microsoft.Data.SqlClient;

namespace TNS_TOEICAdmin.Models
{
    public class DepartmentAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        public static async Task<List<Department>> GetDepartmentsAsync()
        {
            var departments = new List<Department>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string sql = @"
            SELECT d1.DepartmentKey, d1.DepartmentID, d1.DepartmentName, d1.ShortName, d1.Address, 
                   d1.TRCD, d1.ParentKey, d2.ShortName AS ParentShortName, 
                   d1.OriganizationID, d1.OriganizationPath, d1.Rank, d1.Class, d1.ForReport
            FROM [HRM_Department] d1
            LEFT JOIN [HRM_Department] d2 ON d1.ParentKey = d2.DepartmentKey";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            departments.Add(new Department
                            {
                                DepartmentKey = reader.GetGuid(0),
                                DepartmentID = reader.IsDBNull(1) ? null : reader.GetString(1),
                                DepartmentName = reader.IsDBNull(2) ? null : reader.GetString(2),
                                ShortName = reader.IsDBNull(3) ? null : reader.GetString(3),
                                Address = reader.IsDBNull(4) ? null : reader.GetString(4),
                                TRCD = reader.IsDBNull(5) ? null : reader.GetString(5),
                                ParentKey = reader.IsDBNull(6) ? (Guid?)null : reader.GetGuid(6),
                                ParentShortName = reader.IsDBNull(7) ? null : reader.GetString(7),
                                OriganizationID = reader.IsDBNull(8) ? null : reader.GetString(8),
                                OriganizationPath = reader.IsDBNull(9) ? null : reader.GetString(9),
                                Rank = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10),
                                Class = reader.IsDBNull(11) ? null : reader.GetString(11),
                                ForReport = reader.IsDBNull(12) ? null : reader.GetString(12)
                            });
                        }
                    }
                }
            }
            return departments;
        }

        public static async Task AddDepartmentAsync(Department department)
        {
            if (department == null || string.IsNullOrEmpty(department.DepartmentID) || string.IsNullOrEmpty(department.DepartmentName))
            {
                throw new ArgumentException("Dữ liệu phòng ban không hợp lệ.");
            }

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Tính toán OriganizationPath
                string origanizationPath = department.DepartmentKey.ToString();
                if (department.ParentKey.HasValue)
                {
                    string parentPathSql = "SELECT OriganizationPath FROM [HRM_Department] WHERE DepartmentKey = @ParentKey";
                    using (var cmd = new SqlCommand(parentPathSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ParentKey", department.ParentKey.Value);
                        var parentPath = await cmd.ExecuteScalarAsync();
                        if (parentPath != null && parentPath != DBNull.Value)
                        {
                            origanizationPath = $"{parentPath}/{department.DepartmentKey}";
                        }
                    }
                }

                string sql = @"
                    INSERT INTO [HRM_Department] (DepartmentKey, DepartmentID, DepartmentName, ShortName, Address, TRCD, 
                        ParentKey, OriganizationID, OriganizationPath, Rank, Class, ForReport)
                    VALUES (@DepartmentKey, @DepartmentID, @DepartmentName, @ShortName, @Address, @TRCD, 
                        @ParentKey, @OriganizationID, @OriganizationPath, @Rank, @Class, @ForReport)";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@DepartmentKey", department.DepartmentKey);
                    cmd.Parameters.AddWithValue("@DepartmentID", department.DepartmentID);
                    cmd.Parameters.AddWithValue("@DepartmentName", department.DepartmentName);
                    cmd.Parameters.AddWithValue("@ShortName", (object)department.ShortName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Address", (object)department.Address ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@TRCD", (object)department.TRCD ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ParentKey", (object)department.ParentKey ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@OriganizationID", (object)department.OriganizationID ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@OriganizationPath", origanizationPath);
                    cmd.Parameters.AddWithValue("@Rank", (object)department.Rank ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Class", (object)department.Class ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ForReport", (object)department.ForReport ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public static async Task UpdateDepartmentAsync(Department department)
        {
            if (department == null || department.DepartmentKey == Guid.Empty || string.IsNullOrEmpty(department.DepartmentID) || string.IsNullOrEmpty(department.DepartmentName))
            {
                throw new ArgumentException("Dữ liệu phòng ban không hợp lệ.");
            }

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Tính toán lại OriganizationPath
                string origanizationPath = department.DepartmentKey.ToString();
                if (department.ParentKey.HasValue)
                {
                    string parentPathSql = "SELECT OriganizationPath FROM [HRM_Department] WHERE DepartmentKey = @ParentKey";
                    using (var cmd = new SqlCommand(parentPathSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ParentKey", department.ParentKey.Value);
                        var parentPath = await cmd.ExecuteScalarAsync();
                        if (parentPath != null && parentPath != DBNull.Value)
                        {
                            origanizationPath = $"{parentPath}/{department.DepartmentKey}";
                        }
                    }
                }

                string sql = @"
                    UPDATE [HRM_Department]
                    SET DepartmentID = @DepartmentID, DepartmentName = @DepartmentName, ShortName = @ShortName, 
                        Address = @Address, TRCD = @TRCD, ParentKey = @ParentKey, OriganizationID = @OriganizationID, 
                        OriganizationPath = @OriganizationPath, Rank = @Rank, Class = @Class, ForReport = @ForReport
                    WHERE DepartmentKey = @DepartmentKey";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@DepartmentKey", department.DepartmentKey);
                    cmd.Parameters.AddWithValue("@DepartmentID", department.DepartmentID);
                    cmd.Parameters.AddWithValue("@DepartmentName", department.DepartmentName);
                    cmd.Parameters.AddWithValue("@ShortName", (object)department.ShortName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Address", (object)department.Address ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@TRCD", (object)department.TRCD ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ParentKey", (object)department.ParentKey ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@OriganizationID", (object)department.OriganizationID ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@OriganizationPath", origanizationPath);
                    cmd.Parameters.AddWithValue("@Rank", (object)department.Rank ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Class", (object)department.Class ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ForReport", (object)department.ForReport ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Cập nhật OriganizationPath cho các phòng ban con (nếu có)
                await UpdateChildOriganizationPathsAsync(department.DepartmentKey, origanizationPath, conn);
            }
        }

        private static async Task UpdateChildOriganizationPathsAsync(Guid parentKey, string parentPath, SqlConnection conn)
        {
            string sql = "SELECT DepartmentKey FROM [HRM_Department] WHERE ParentKey = @ParentKey";
            var childKeys = new List<Guid>();
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@ParentKey", parentKey);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        childKeys.Add(reader.GetGuid(0));
                    }
                }
            }

            foreach (var childKey in childKeys)
            {
                string newPath = $"{parentPath}/{childKey}";
                string updateSql = "UPDATE [HRM_Department] SET OriganizationPath = @OriganizationPath WHERE DepartmentKey = @DepartmentKey";
                using (var cmd = new SqlCommand(updateSql, conn))
                {
                    cmd.Parameters.AddWithValue("@OriganizationPath", newPath);
                    cmd.Parameters.AddWithValue("@DepartmentKey", childKey);
                    await cmd.ExecuteNonQueryAsync();
                }
                await UpdateChildOriganizationPathsAsync(childKey, newPath, conn); // Đệ quy cho các phòng ban con
            }
        }

        //public static async Task<bool> CanDeleteDepartmentAsync(Guid departmentKey)
        //{
        //    using (var conn = new SqlConnection(_connectionString))
        //    {
        //        await conn.OpenAsync();

        //        Kiểm tra xem có phòng ban con không
        //        string childSql = "SELECT COUNT(*) FROM [HRM_Department] WHERE ParentKey = @DepartmentKey";
        //        using (var cmd = new SqlCommand(childSql, conn))
        //        {
        //            cmd.Parameters.AddWithValue("@DepartmentKey", departmentKey);
        //            int childCount = (int)await cmd.ExecuteScalarAsync();
        //            if (childCount > 0) return false;
        //        }

        //        Kiểm tra xem có nhân viên nào thuộc phòng ban này không
        //        string employeeSql = "SELECT COUNT(*) FROM [HRM_Employee] WHERE DepartmentKey = @DepartmentKey";
        //        using (var cmd = new SqlCommand(employeeSql, conn))
        //        {
        //            cmd.Parameters.AddWithValue("@DepartmentKey", departmentKey);
        //            int employeeCount = (int)await cmd.ExecuteScalarAsync();
        //            if (employeeCount > 0) return false;
        //        }

        //        return true;
        //    }
        //}

        public static async Task DeleteDepartmentAsync(Guid departmentKey)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string sql = "DELETE FROM [HRM_Department] WHERE DepartmentKey = @DepartmentKey";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@DepartmentKey", departmentKey);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }

    public class Department
    {
        public Guid DepartmentKey { get; set; }
        public string DepartmentID { get; set; }
        public string DepartmentName { get; set; }
        public string? ShortName { get; set; }
        public string? Address { get; set; }
        public string? TRCD { get; set; }
        public Guid? ParentKey { get; set; }
        public string? ParentShortName { get; set; }
        public string? OriganizationID { get; set; }
        public string? OriganizationPath { get; set; }
        public int? Rank { get; set; }
        public string? Class { get; set; }
        public string? ForReport { get; set; } // Sửa thành bool? để phù hợp với checkbox
    }
}