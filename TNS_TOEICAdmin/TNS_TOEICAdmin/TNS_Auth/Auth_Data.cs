using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TNS_Auth
{
    public class Securiry
    {
        #region [ Securiry ]
        public static DataRow UserNameLogin(string UserName, out string MessageError)
        {
            MessageError = "";
            string zSQL = "SELECT A.UserKey, A.UserName, A.Password,A.Activate,A.EmployeeKey,B.EmployeeID, B.LastName + ' ' + B.FirstName  AS EmployeeName "
                        + "FROM[dbo].[SYS_Users] A "
                        + "LEFT JOIN[dbo].[HRM_Employee] B ON A.EmployeeKey = B.EmployeeKey "
                        + "WHERE A.UserName = @UserName ";
            DataTable zTable = new DataTable();
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            try
            {
                SqlConnection zConnect = new SqlConnection(zConnectionString);
                zConnect.Open();
                SqlCommand zCommand = new SqlCommand(zSQL, zConnect);
                zCommand.Parameters.Add("@UserName", SqlDbType.NVarChar).Value = UserName;
                SqlDataAdapter zAdapter = new SqlDataAdapter(zCommand);
                zAdapter.Fill(zTable);
                zCommand.Dispose();
                zConnect.Close();
            }
            catch (Exception ex)
            {
                MessageError = ex.ToString();
            }
            if (zTable.Rows.Count == 1)
            {
                MessageError = "OK";
                return zTable.Rows[0];
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region [ Update log ]
        public static string UpdateFailedPass(string UserName)
        {
            string zResult = "";
            //---------- String SQL Access Database ---------------
            string zSQL = @"UPDATE [dbo].[SYS_Users] SET  FailedPasswordAttemptCount = FailedPasswordAttemptCount + 1 WHERE UserName = @UserName";
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            SqlConnection zConnect = new SqlConnection(zConnectionString);
            zConnect.Open();

            try
            {
                SqlCommand zCommand = new SqlCommand(zSQL, zConnect);
                zCommand.CommandType = CommandType.Text;
                zCommand.Parameters.Add("@UserName", SqlDbType.NVarChar).Value = UserName;
                zResult = zCommand.ExecuteNonQuery().ToString();
                zCommand.Dispose();

            }
            catch (Exception Err)
            {
                zResult = Err.ToString();
            }
            finally
            {
                zConnect.Close();
            }
            return zResult;
        }
        public static string UpdateLastLogin(string UserName)
        {
            string zResult = "";
            //---------- String SQL Access Database ---------------
            string zSQL = @"UPDATE [dbo].[SYS_Users] SET  LastLoginDate = GetDate() WHERE UserName = @UserName";
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            SqlConnection zConnect = new SqlConnection(zConnectionString);
            zConnect.Open();

            try
            {
                SqlCommand zCommand = new SqlCommand(zSQL, zConnect);
                zCommand.CommandType = CommandType.Text;
                zCommand.Parameters.Add("@UserName", SqlDbType.NVarChar).Value = UserName;
                zResult = zCommand.ExecuteNonQuery().ToString();
                zCommand.Dispose();

            }
            catch (Exception Err)
            {
                zResult = Err.ToString();
            }
            finally
            {
                zConnect.Close();
            }
            return zResult;
        }
        #endregion
    }

    public class MyCryptography
    {
        public static string HashPass(string nPass)
        {
            string salt = BCrypt.Net.BCrypt.GenerateSalt(12); // 12 là số vòng hash

            // Dùng salt này để mã hóa
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(nPass, salt);

            // Có thể đặt breakpoint ở đây để xem plainTextPassword, salt, và hashedPassword
            return hashedPassword;
        }

        public static Boolean VerifyHash(string NewPass, string OldHashedPass)
        {
            return BCrypt.Net.BCrypt.Verify(NewPass, OldHashedPass);
        }

    }
    public class MyCryptographyMembers
    {
        public static string HashPassMember(string nPass)
        {
            // Gọi đến hàm mã hóa BCrypt an toàn đã có sẵn
            return TNS_Auth.MyCryptography.HashPass(nPass);
        }

        public static Boolean VerifyHashMember(string NewPass, string OldHashedPass)
        {
            // Gọi đến hàm xác thực BCrypt an toàn đã có sẵn
            return TNS_Auth.MyCryptography.VerifyHash(NewPass, OldHashedPass);
        }
    }
}
