using Microsoft.Data.SqlClient;
using TNS_Auth;

namespace TNS_TOEICAdmin.Models
{
    public class UserAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        public static async Task<List<User>> GetUsersAsync(int page = 1, int pageSize = 10, string search = null, string activate = null)
        {
            var users = new List<User>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = @"
            SELECT u.UserKey, u.UserName, u.Description, u.Activate, u.LastLoginDate, 
                   u.FailedPasswordAttemptCount, e.FirstName + ' ' + e.LastName AS EmployeeName,
                   u.EmployeeKey, u.ExpireDate,
                   r.RoleKey, r.RoleID, r.RoleName, ur.RoleRead, ur.RoleEdit, ur.RoleAdd, ur.RoleDel, ur.RoleApproval
            FROM [SYS_Users] u
            LEFT JOIN [HRM_Employee] e ON u.EmployeeKey = e.EmployeeKey
            LEFT JOIN [SYS_Users_Roles] ur ON u.UserKey = ur.UserKey
            LEFT JOIN [SYS_Roles] r ON ur.RoleKey = r.RoleKey
            WHERE 1=1";

                // Thêm điều kiện tìm kiếm nếu có
                if (!string.IsNullOrEmpty(search))
                {
                    sql += " AND u.UserName LIKE @Search";
                }
                if (!string.IsNullOrEmpty(activate))
                {
                    sql += " AND u.Activate = @Activate";
                }

                // Thêm phân trang
                sql += " ORDER BY u.UserName OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    if (!string.IsNullOrEmpty(search))
                    {
                        cmd.Parameters.AddWithValue("@Search", "%" + search + "%");
                    }
                    if (!string.IsNullOrEmpty(activate))
                    {
                        cmd.Parameters.AddWithValue("@Activate", activate == "1");
                    }
                    cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        var userDict = new Dictionary<Guid, User>();
                        while (await reader.ReadAsync())
                        {
                            var userKey = reader.GetGuid(0);
                            if (!userDict.ContainsKey(userKey))
                            {
                                userDict[userKey] = new User
                                {
                                    UserKey = userKey,
                                    UserName = reader.GetString(1),
                                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    Activate = reader.GetBoolean(3),
                                    LastLoginDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4).ToString("yyyy-MM-dd HH:mm:ss"),
                                    FailedPasswordAttemptCount = reader.GetInt32(5),
                                    EmployeeName = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    EmployeeKey = reader.IsDBNull(7) ? null : reader.GetGuid(7),
                                    ExpireDate = reader.IsDBNull(8) ? null : reader.GetDateTime(8).ToString("yyyy-MM-dd HH:mm:ss"),
                                    Roles = new List<UserRole>()
                                };
                            }

                            if (!reader.IsDBNull(9)) // RoleKey bắt đầu từ cột 9
                            {
                                userDict[userKey].Roles.Add(new UserRole
                                {
                                    RoleKey = reader.GetGuid(9),
                                    RoleID = reader.GetString(10),
                                    RoleName = reader.GetString(11),
                                    RoleRead = reader.GetBoolean(12),
                                    RoleEdit = reader.GetBoolean(13),
                                    RoleAdd = reader.GetBoolean(14),
                                    RoleDel = reader.GetBoolean(15),
                                    RoleApproval = reader.GetBoolean(16)
                                });
                            }
                        }
                        users = userDict.Values.ToList();
                    }
                }
            }
            return users;
        }
        public static async Task<List<Employee>> GetEmployeesAsync()
        {
            var employees = new List<Employee>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = "SELECT EmployeeKey, FirstName, LastName FROM [HRM_Employee]";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            employees.Add(new Employee
                            {
                                EmployeeKey = reader.GetGuid(0),
                                FirstName = reader.GetString(1),
                                LastName = reader.GetString(2)
                            });
                        }
                    }
                }
            }
            return employees;
        }
        public static async Task<List<UserRole>> GetAllRolesAsync()
        {
            var roles = new List<UserRole>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = "SELECT RoleKey, RoleID, RoleName FROM [SYS_Roles]";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            roles.Add(new UserRole
                            {
                                RoleKey = reader.GetGuid(0),
                                RoleID = reader.GetString(1),
                                RoleName = reader.GetString(2)
                            });
                        }
                    }
                }
            }
            return roles;
        }

        public static async Task AddUserAsync(User user)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = @"
            INSERT INTO [SYS_Users] (UserKey, UserName, Password, Description, EmployeeKey, Activate, ExpireDate, FailedPasswordAttemptCount, CreatedBy)
            VALUES (@UserKey, @UserName, @Password, @Description, @EmployeeKey, @Activate, @ExpireDate, 0, @CreatedBy)";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserKey", user.UserKey);
                    cmd.Parameters.AddWithValue("@UserName", user.UserName);
                    cmd.Parameters.AddWithValue("@Password", MyCryptography.HashPass(user.Password)); // Mã hóa tại đây
                    cmd.Parameters.AddWithValue("@Description", (object)user.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@EmployeeKey", (object)user.EmployeeKey ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Activate", user.Activate);
                    cmd.Parameters.AddWithValue("@ExpireDate", (object)user.ExpireDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CreatedBy", user.CreatedBy);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public static async Task UpdateUserAsync(User user)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = @"
            UPDATE [SYS_Users]
            SET UserName = @UserName, 
                Password = CASE WHEN @Password IS NOT NULL THEN @Password ELSE Password END, 
                Description = @Description, 
                EmployeeKey = @EmployeeKey, 
                Activate = @Activate, 
                ExpireDate = @ExpireDate,
                ModifiedBy = @ModifiedBy
            WHERE UserKey = @UserKey";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserKey", user.UserKey);
                    cmd.Parameters.AddWithValue("@UserName", user.UserName);
                    cmd.Parameters.AddWithValue("@Password", (object)(user.Password != null ? MyCryptography.HashPass(user.Password) : null) ?? DBNull.Value); // Mã hóa nếu có
                    cmd.Parameters.AddWithValue("@Description", (object)user.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@EmployeeKey", (object)user.EmployeeKey ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Activate", user.Activate);
                    cmd.Parameters.AddWithValue("@ExpireDate", (object)user.ExpireDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ModifiedBy", user.ModifiedBy);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public static async Task DeleteUserAsync(Guid userKey)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = "DELETE FROM [SYS_Users_Roles] WHERE UserKey = @UserKey; DELETE FROM [SYS_Users] WHERE UserKey = @UserKey";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserKey", userKey);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public static async Task UpdateUserRolesAsync(Guid userKey, List<UserRole> roles)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                // Xóa các vai trò cũ
                string deleteSql = "DELETE FROM [SYS_Users_Roles] WHERE UserKey = @UserKey";
                using (var cmd = new SqlCommand(deleteSql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserKey", userKey);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Thêm các vai trò mới
                string insertSql = @"
                    INSERT INTO [SYS_Users_Roles] (UserKey, RoleKey, RoleRead, RoleEdit, RoleAdd, RoleDel, RoleApproval)
                    VALUES (@UserKey, @RoleKey, @RoleRead, @RoleEdit, @RoleAdd, @RoleDel, @RoleApproval)";
                foreach (var role in roles)
                {
                    using (var cmd = new SqlCommand(insertSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserKey", userKey);
                        cmd.Parameters.AddWithValue("@RoleKey", role.RoleKey);
                        cmd.Parameters.AddWithValue("@RoleRead", role.RoleRead);
                        cmd.Parameters.AddWithValue("@RoleEdit", role.RoleEdit);
                        cmd.Parameters.AddWithValue("@RoleAdd", role.RoleAdd);
                        cmd.Parameters.AddWithValue("@RoleDel", role.RoleDel);
                        cmd.Parameters.AddWithValue("@RoleApproval", role.RoleApproval);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }
    }

    public class User
    {
        public Guid UserKey { get; set; }
        public string UserName { get; set; }
        public string? Password { get; set; }
        public string? Description { get; set; }
        public Guid? EmployeeKey { get; set; }
        public bool Activate { get; set; }
        public string? ExpireDate { get; set; }
        public string? LastLoginDate { get; set; }
        public int FailedPasswordAttemptCount { get; set; }
        public string? EmployeeName { get; set; }
        public Guid CreatedBy { get; set; }
        public Guid ModifiedBy { get; set; }
        public List<UserRole> Roles { get; set; }
    }

    public class Employee
    {
        public Guid EmployeeKey { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }

    public class UserRole
    {
        public Guid RoleKey { get; set; }
        public string RoleID { get; set; }
        public string RoleName { get; set; }
        public bool RoleRead { get; set; }
        public bool RoleEdit { get; set; }
        public bool RoleAdd { get; set; }
        public bool RoleDel { get; set; }
        public bool RoleApproval { get; set; }
    }
}