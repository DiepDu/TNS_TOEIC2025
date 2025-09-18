using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace TNS_TOEICAdmin.DataAccess
{
    public class EmployeeProfileAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        static EmployeeProfileAccessData()
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new InvalidOperationException("Connection string is null or empty.");
            }
            Console.WriteLine($"Initialized connection string: {_connectionString}");
        }

        public static EmployeeProfile GetEmployeeProfile(string employeeKey)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string query = @"
                        SELECT e.EmployeeID, e.LastName, e.FirstName, e.DepartmentKey, d.DepartmentName, 
                               e.CompanyEmail, e.StartingDate, e.LeavingDate, e.PhotoPath
                        FROM HRM_Employee e
                        LEFT JOIN HRM_Department d ON e.DepartmentKey = d.DepartmentKey
                        WHERE e.EmployeeKey = @EmployeeKey";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@EmployeeKey", Guid.Parse(employeeKey));
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new EmployeeProfile
                                {
                                    EmployeeID = reader["EmployeeID"].ToString(),
                                    LastName = reader["LastName"].ToString(),
                                    FirstName = reader["FirstName"].ToString(),
                                    DepartmentKey = reader.IsDBNull("DepartmentKey") ? (Guid?)null : reader.GetGuid("DepartmentKey"),
                                    DepartmentName = reader.IsDBNull("DepartmentName") ? "Chưa có" : reader["DepartmentName"].ToString(),
                                    CompanyEmail = reader["CompanyEmail"].ToString(),
                                    StartingDate = reader.IsDBNull("StartingDate") ? (DateTime?)null : reader.GetDateTime("StartingDate"),
                                    LeavingDate = reader.IsDBNull("LeavingDate") ? (DateTime?)null : reader.GetDateTime("LeavingDate"),
                                    PhotoPath = reader["PhotoPath"]?.ToString()
                                };
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching employee profile for EmployeeKey {employeeKey}: {ex.Message}");
                }
                return null;
            }
        }

        public static bool UpdateEmployeeProfile(string employeeKey, string lastName, string firstName)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string query = "UPDATE HRM_Employee SET LastName = @LastName, FirstName = @FirstName WHERE EmployeeKey = @EmployeeKey";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@LastName", lastName ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@FirstName", firstName ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@EmployeeKey", Guid.Parse(employeeKey));
                        int rowsAffected = command.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating employee profile for EmployeeKey {employeeKey}: {ex.Message}");
                    return false;
                }
            }
        }

        public static string GetEmployeeAvatar(string employeeKey)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string query = "SELECT PhotoPath FROM HRM_Employee WHERE EmployeeKey = @EmployeeKey";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@EmployeeKey", Guid.Parse(employeeKey));
                        object result = command.ExecuteScalar();
                        return result?.ToString();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching PhotoPath for EmployeeKey {employeeKey}: {ex.Message}");
                    return null;
                }
            }
        }

        public static bool UpdateEmployeeAvatar(string employeeKey, string photoPath)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string query = "UPDATE HRM_Employee SET PhotoPath = @PhotoPath WHERE EmployeeKey = @EmployeeKey";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@PhotoPath", photoPath ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@EmployeeKey", Guid.Parse(employeeKey));
                        int rowsAffected = command.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating PhotoPath for EmployeeKey {employeeKey}: {ex.Message}");
                    return false;
                }
            }
        }

        public static string GetUserPassword(string employeeKey)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string query = "SELECT Password FROM SYS_Users WHERE EmployeeKey = @EmployeeKey";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@EmployeeKey", Guid.Parse(employeeKey));
                        object result = command.ExecuteScalar();
                        return result?.ToString();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching password for EmployeeKey {employeeKey}: {ex.Message}");
                    return null;
                }
            }
        }

        public static bool UpdateUserPassword(string employeeKey, string newPassword)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string query = "UPDATE SYS_Users SET Password = @Password WHERE EmployeeKey = @EmployeeKey";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Password", newPassword);
                        command.Parameters.AddWithValue("@EmployeeKey", Guid.Parse(employeeKey));
                        int rowsAffected = command.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating password for EmployeeKey {employeeKey}: {ex.Message}");
                    return false;
                }
            }
        }
    }

    public class EmployeeProfile
    {
        public string EmployeeID { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public Guid? DepartmentKey { get; set; }
        public string DepartmentName { get; set; }
        public string CompanyEmail { get; set; }
        public DateTime? StartingDate { get; set; }
        public DateTime? LeavingDate { get; set; }
        public string PhotoPath { get; set; }
    }
}