using Microsoft.Extensions.Configuration;
using System;
using System.Data.SqlClient;

namespace TNS_TOEICTest.DataAccess
{
    public class CreateAccountAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        public bool CheckEmailExists(string email)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string query = "SELECT COUNT(*) FROM EDU_Member WHERE MemberID = @MemberID";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@MemberID", email);
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

        public bool CreateMember(Guid memberKey, string memberID, string memberName, int gender, int yearOld, DateTime createOn, Guid createBy, string createName, int active, string password)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string query = @"
                        INSERT INTO EDU_Member (MemberKey, MemberID, MemberName, Gender, YearOld, CreatedOn, CreatedBy, CreatedName, Activate, Password)
                        VALUES (@MemberKey, @MemberID, @MemberName, @Gender, @YearOld, @CreateOn, @CreateBy, @CreateName, @Active, @Password)";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@MemberKey", memberKey);
                        command.Parameters.AddWithValue("@MemberID", memberID);
                        command.Parameters.AddWithValue("@MemberName", memberName);
                        command.Parameters.AddWithValue("@Gender", gender);
                        command.Parameters.AddWithValue("@YearOld", yearOld);
                        command.Parameters.AddWithValue("@CreateOn", createOn);
                        command.Parameters.AddWithValue("@CreateBy", createBy);
                        command.Parameters.AddWithValue("@CreateName", createName);
                        command.Parameters.AddWithValue("@Active", active);
                        command.Parameters.AddWithValue("@Password", password);

                        int rowsAffected = command.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating member: {ex.Message}");
                    return false;
                }
            }
        }
    }
}