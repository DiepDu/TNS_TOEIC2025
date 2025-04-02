using Microsoft.Extensions.Configuration;
using System;
using System.Data.SqlClient;

namespace TNS_TOEICTest.DataAccess
{
    public class ProfileAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        public UserProfile GetUserProfile(string memberKey)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string query = "SELECT MemberName, MemberID, Gender, YearOld, CreatedOn, Avatar FROM EDU_Member WHERE MemberKey = @MemberKey";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@MemberKey", Guid.Parse(memberKey));
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new UserProfile
                                {
                                    MemberName = reader["MemberName"].ToString(),
                                    MemberID = reader["MemberID"].ToString(),
                                    Gender = Convert.ToInt32(reader["Gender"]),
                                    YearOld = Convert.ToInt32(reader["YearOld"]),
                                    CreatedOn = Convert.ToDateTime(reader["CreatedOn"]),
                                    Avatar = reader["Avatar"]?.ToString()
                                };
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching user profile: {ex.Message}");
                }
                return null;
            }
        }

        public bool CheckEmailExists(string email, string memberKey)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string query = "SELECT COUNT(*) FROM EDU_Member WHERE MemberID = @MemberID AND MemberKey != @MemberKey";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@MemberID", email);
                        command.Parameters.AddWithValue("@MemberKey", Guid.Parse(memberKey));
                        int count = (int)command.ExecuteScalar();
                        return count > 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error checking email: {ex.Message}");
                    return false;
                }
            }
        }

        public bool UpdateUserProfile(string memberKey, string memberName, int gender, int yearOld)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string query = "UPDATE EDU_Member SET MemberName = @MemberName, Gender = @Gender, YearOld = @YearOld WHERE MemberKey = @MemberKey";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@MemberName", memberName);
                        command.Parameters.AddWithValue("@Gender", gender);
                        command.Parameters.AddWithValue("@YearOld", yearOld);
                        command.Parameters.AddWithValue("@MemberKey", Guid.Parse(memberKey));
                        int rowsAffected = command.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating user profile: {ex.Message}");
                    return false;
                }
            }
        }

        public string GetUserAvatar(string memberKey)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string query = "SELECT Avatar FROM EDU_Member WHERE MemberKey = @MemberKey";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@MemberKey", Guid.Parse(memberKey));
                        object result = command.ExecuteScalar();
                        return result?.ToString();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching user avatar: {ex.Message}");
                    return null;
                }
            }
        }

        public bool UpdateUserAvatar(string memberKey, string avatarPath)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string query = "UPDATE EDU_Member SET Avatar = @Avatar WHERE MemberKey = @MemberKey";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Avatar", avatarPath);
                        command.Parameters.AddWithValue("@MemberKey", Guid.Parse(memberKey));
                        int rowsAffected = command.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating user avatar: {ex.Message}");
                    return false;
                }
            }
        }

        public string GetUserPassword(string memberKey)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string query = "SELECT Password FROM EDU_Member WHERE MemberKey = @MemberKey";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@MemberKey", Guid.Parse(memberKey));
                        object result = command.ExecuteScalar();
                        return result?.ToString();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching user password: {ex.Message}");
                    return null;
                }
            }
        }

        public bool UpdateUserPassword(string memberKey, string newPassword)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string query = "UPDATE EDU_Member SET Password = @Password WHERE MemberKey = @MemberKey";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Password", newPassword);
                        command.Parameters.AddWithValue("@MemberKey", Guid.Parse(memberKey));
                        int rowsAffected = command.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating user password: {ex.Message}");
                    return false;
                }
            }
        }
    }

    public class UserProfile
    {
        public string MemberName { get; set; }
        public string MemberID { get; set; }
        public int Gender { get; set; }
        public int YearOld { get; set; }
        public DateTime CreatedOn { get; set; }
        public string Avatar { get; set; }
    }
}