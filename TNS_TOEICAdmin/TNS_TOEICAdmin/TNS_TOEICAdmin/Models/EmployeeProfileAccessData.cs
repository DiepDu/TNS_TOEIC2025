using System;
using System.Data;
using System.Data.SqlClient;
using TNS_TOEICAdmin.Pages.Account;

namespace TNS_TOEICAdmin.DataAccess
{
    public class EmployeeProfileAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        public EmployeeProfile GetEmployeeProfile(string employeeKey)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string query = @"
                        SELECT e.EmployeeID, e.LastName, e.FirstName, e.DepartmentKey, d.DepartmentName, 
                               e.CompanyEmail, e.StartingDate, e.LeavingDate, e.Avatar
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
                                    Avatar = reader["Avatar"]?.ToString()
                                };
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching employee profile: {ex.Message}");
                }
                return null;
            }
        }

        public bool UpdateEmployeeProfile(string employeeKey, string lastName, string firstName)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string query = "UPDATE HRM_Employee SET LastName = @LastName, FirstName = @FirstName WHERE EmployeeKey = @EmployeeKey";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@LastName", lastName);
                        command.Parameters.AddWithValue("@FirstName", firstName);
                        command.Parameters.AddWithValue("@EmployeeKey", Guid.Parse(employeeKey));
                        int rowsAffected = command.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating employee profile: {ex.Message}");
                    return false;
                }
            }
        }

        public string GetEmployeeAvatar(string employeeKey)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string query = "SELECT Avatar FROM HRM_Employee WHERE EmployeeKey = @EmployeeKey";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@EmployeeKey", Guid.Parse(employeeKey));
                        object result = command.ExecuteScalar();
                        return result?.ToString();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching employee avatar: {ex.Message}");
                    return null;
                }
            }
        }

        public bool UpdateEmployeeAvatar(string employeeKey, string avatarPath)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string query = "UPDATE HRM_Employee SET Avatar = @Avatar WHERE EmployeeKey = @EmployeeKey";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Avatar", avatarPath);
                        command.Parameters.AddWithValue("@EmployeeKey", Guid.Parse(employeeKey));
                        int rowsAffected = command.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating employee avatar: {ex.Message}");
                    return false;
                }
            }
        }
    }
}