using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace TNS_TOEICAdmin.Models
{
    public class EmployeeEntity
    {
        private Guid? _departmentKey;
        private DateTime? _startingDate;
        private DateTime? _leavingDate;

        public Guid EmployeeKey { get; set; }
        public string EmployeeID { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string DepartmentName { get; set; }
        public string CompanyEmail { get; set; }
        public DateTime? CreatedOn { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public Guid? ModifiedBy { get; set; }

        public Guid? DepartmentKey
        {
            get => _departmentKey;
            set
            {
                if (value == null)
                {
                    _departmentKey = null;
                }
                else if (value is Guid guidValue)
                {
                    _departmentKey = guidValue;
                }
                else
                {
                    string stringValue = value?.ToString();
                    if (!string.IsNullOrEmpty(stringValue) && Guid.TryParse(stringValue, out Guid parsedGuid))
                    {
                        _departmentKey = parsedGuid;
                    }
                    else
                    {
                        _departmentKey = null;
                    }
                }
            }
        }

        public DateTime? StartingDate
        {
            get => _startingDate;
            set
            {
                if (value == null)
                {
                    _startingDate = null;
                }
                else if (value is DateTime dateTimeValue)
                {
                    _startingDate = dateTimeValue;
                }
                else
                {
                    string stringValue = value?.ToString();
                    if (!string.IsNullOrEmpty(stringValue) && DateTime.TryParse(stringValue, out DateTime parsedDate))
                    {
                        _startingDate = parsedDate;
                    }
                    else
                    {
                        _startingDate = null;
                    }
                }
            }
        }

        public DateTime? LeavingDate
        {
            get => _leavingDate;
            set
            {
                if (value == null)
                {
                    _leavingDate = null;
                }
                else if (value is DateTime dateTimeValue)
                {
                    _leavingDate = dateTimeValue;
                }
                else
                {
                    string stringValue = value?.ToString();
                    if (!string.IsNullOrEmpty(stringValue) && DateTime.TryParse(stringValue, out DateTime parsedDate))
                    {
                        _leavingDate = parsedDate;
                    }
                    else
                    {
                        _leavingDate = null;
                    }
                }
            }
        }
    }

    public class DepartmentEntity
    {
        public Guid DepartmentKey { get; set; }
        public string DepartmentName { get; set; }
    }

    public class EmployeeAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        public static async Task<(List<EmployeeEntity> Employees, int TotalRecords)> GetEmployeesAsync(int offset, int pageSize, string search, string status)
        {
            var employees = new List<EmployeeEntity>();
            int totalRecords = 0;

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Đếm tổng số bản ghi
                string countSql = @"
            SELECT COUNT(*)
            FROM HRM_Employee e
            WHERE 1=1";
                if (status == "active")
                {
                    countSql += " AND e.LeavingDate IS NULL";
                }
                else if (status == "inactive")
                {
                    countSql += " AND e.LeavingDate IS NOT NULL";
                }
                if (!string.IsNullOrEmpty(search))
                {
                    countSql += " AND (e.LastName LIKE '%' + @Search + '%' OR e.FirstName LIKE '%' + @Search + '%')";
                }

                using (var countCmd = new SqlCommand(countSql, conn))
                {
                    if (!string.IsNullOrEmpty(search))
                    {
                        countCmd.Parameters.AddWithValue("@Search", search);
                    }
                    totalRecords = (int)await countCmd.ExecuteScalarAsync();
                }

                // Lấy danh sách nhân viên
                string sql = @"
            SELECT e.EmployeeKey, e.EmployeeID, e.LastName, e.FirstName, e.DepartmentKey, d.DepartmentName, 
                   e.CompanyEmail, e.StartingDate, e.LeavingDate
            FROM HRM_Employee e
            LEFT JOIN HRM_Department d ON e.DepartmentKey = d.DepartmentKey
            WHERE 1=1";
                if (status == "active")
                {
                    sql += " AND e.LeavingDate IS NULL";
                }
                else if (status == "inactive")
                {
                    sql += " AND e.LeavingDate IS NOT NULL";
                }
                if (!string.IsNullOrEmpty(search))
                {
                    sql += " AND (e.LastName LIKE '%' + @Search + '%' OR e.FirstName LIKE '%' + @Search + '%')";
                }
                sql += " ORDER BY e.CreatedOn DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    if (!string.IsNullOrEmpty(search))
                    {
                        cmd.Parameters.AddWithValue("@Search", search);
                    }
                    cmd.Parameters.AddWithValue("@Offset", offset); // Sử dụng offset thay vì tính toán từ page
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            employees.Add(new EmployeeEntity
                            {
                                EmployeeKey = reader.GetGuid("EmployeeKey"),
                                EmployeeID = reader.GetString("EmployeeID"),
                                LastName = reader.GetString("LastName"),
                                FirstName = reader.GetString("FirstName"),
                                DepartmentKey = reader.IsDBNull("DepartmentKey") ? (Guid?)null : reader.GetGuid("DepartmentKey"),
                                DepartmentName = reader.IsDBNull("DepartmentName") ? null : reader.GetString("DepartmentName"),
                                CompanyEmail = reader.IsDBNull("CompanyEmail") ? null : reader.GetString("CompanyEmail"),
                                StartingDate = reader.IsDBNull("StartingDate") ? (DateTime?)null : reader.GetDateTime("StartingDate"),
                                LeavingDate = reader.IsDBNull("LeavingDate") ? (DateTime?)null : reader.GetDateTime("LeavingDate")
                            });
                        }
                    }
                }
            }
            return (employees, totalRecords);
        }

        public static async Task<List<DepartmentEntity>> GetDepartmentsAsync()
        {
            var departments = new List<DepartmentEntity>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = "SELECT DepartmentKey, DepartmentName FROM HRM_Department";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            departments.Add(new DepartmentEntity
                            {
                                DepartmentKey = reader.GetGuid(0),
                                DepartmentName = reader.IsDBNull(1) ? null : reader.GetString(1)
                            });
                        }
                    }
                }
            }
            return departments;
        }

        public static async Task<bool> CreateEmployeeAsync(EmployeeEntity employee)
        {
            if (employee == null || string.IsNullOrEmpty(employee.EmployeeID) ||
                string.IsNullOrEmpty(employee.LastName) || string.IsNullOrEmpty(employee.FirstName))
            {
                return false;
            }

            if (employee.EmployeeKey == Guid.Empty)
            {
                employee.EmployeeKey = Guid.NewGuid();
            }

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = @"
                    INSERT INTO HRM_Employee (EmployeeKey, EmployeeID, LastName, FirstName, DepartmentKey, CompanyEmail, 
                        StartingDate, LeavingDate, CreatedOn, CreatedBy, ModifiedOn, ModifiedBy)
                    VALUES (@EmployeeKey, @EmployeeID, @LastName, @FirstName, @DepartmentKey, @CompanyEmail, 
                        @StartingDate, @LeavingDate, @CreatedOn, @CreatedBy, @ModifiedOn, @ModifiedBy)";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@EmployeeKey", employee.EmployeeKey);
                    cmd.Parameters.AddWithValue("@EmployeeID", employee.EmployeeID ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@LastName", employee.LastName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@FirstName", employee.FirstName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DepartmentKey", employee.DepartmentKey ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@CompanyEmail", employee.CompanyEmail ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@StartingDate", employee.StartingDate ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@LeavingDate", employee.LeavingDate ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@CreatedOn", employee.CreatedOn ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@CreatedBy", employee.CreatedBy ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ModifiedOn", employee.ModifiedOn ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ModifiedBy", employee.ModifiedBy ?? (object)DBNull.Value);

                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        public static async Task UpdateEmployeeAsync(EmployeeEntity employee)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = @"
                    UPDATE HRM_Employee
                    SET EmployeeID = @EmployeeID, LastName = @LastName, FirstName = @FirstName, 
                        DepartmentKey = @DepartmentKey, StartingDate = @StartingDate, 
                        LeavingDate = @LeavingDate, CompanyEmail = @CompanyEmail, 
                        ModifiedOn = @ModifiedOn, ModifiedBy = @ModifiedBy
                    WHERE EmployeeKey = @EmployeeKey";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@EmployeeKey", employee.EmployeeKey);
                    cmd.Parameters.AddWithValue("@EmployeeID", employee.EmployeeID);
                    cmd.Parameters.AddWithValue("@LastName", employee.LastName);
                    cmd.Parameters.AddWithValue("@FirstName", employee.FirstName);
                    cmd.Parameters.AddWithValue("@DepartmentKey", (object)employee.DepartmentKey ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@StartingDate", (object)employee.StartingDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@LeavingDate", (object)employee.LeavingDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CompanyEmail", employee.CompanyEmail);
                    cmd.Parameters.AddWithValue("@ModifiedOn", employee.ModifiedOn ?? DateTime.Now);
                    cmd.Parameters.AddWithValue("@ModifiedBy", employee.ModifiedBy ?? Guid.Empty);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public static async Task DeleteEmployeeAsync(Guid employeeKey)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = "DELETE FROM HRM_Employee WHERE EmployeeKey = @EmployeeKey";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@EmployeeKey", employeeKey);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public static async Task SoftDeleteEmployeeAsync(Guid employeeKey)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = "UPDATE HRM_Employee SET LeavingDate = @LeavingDate, ModifiedOn = @ModifiedOn WHERE EmployeeKey = @EmployeeKey";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@EmployeeKey", employeeKey);
                    cmd.Parameters.AddWithValue("@LeavingDate", DateTime.Now);
                    cmd.Parameters.AddWithValue("@ModifiedOn", DateTime.Now);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}