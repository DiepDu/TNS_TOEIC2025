using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TNS.Member
{
    public class AccessData
    {


    }

    public class Securiry
    {
        #region [ Securiry ]
        public static DataRow MemberLogin(string MemberID, out string MessageError)
        {
            MessageError = "";
            string zSQL = "SELECT MemberKey, MemberName, Avatar, Password,Activate "
                        + "FROM[dbo].[EDU_Member]  "
                        + "WHERE MemberID = @MemberID AND Activate = 1 ";
            DataTable zTable = new DataTable();
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            try
            {
                SqlConnection zConnect = new SqlConnection(zConnectionString);
                zConnect.Open();
                SqlCommand zCommand = new SqlCommand(zSQL, zConnect);
                zCommand.Parameters.Add("@MemberID", SqlDbType.NVarChar).Value = MemberID;
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
        public static string UpdateFailedPass(string MemberID)
        {
            string zResult = "";
            //---------- String SQL Access Database ---------------
            string zSQL = @"UPDATE [dbo].[EDU_Member] SET  FailedPasswordAttemptCount = FailedPasswordAttemptCount + 1 WHERE MemberID = @MemberID";
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            SqlConnection zConnect = new SqlConnection(zConnectionString);
            zConnect.Open();

            try
            {
                SqlCommand zCommand = new SqlCommand(zSQL, zConnect);
                zCommand.CommandType = CommandType.Text;
                zCommand.Parameters.Add("@MemberID", SqlDbType.NVarChar).Value = MemberID;
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
        public static string UpdateLastLogin(string MemberID)
        {
            string zResult = "";
            //---------- String SQL Access Database ---------------
            string zSQL = @"UPDATE [dbo].[EDU_Member] SET  LastLoginDate = GetDate() WHERE MemberID = @MemberID";
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            SqlConnection zConnect = new SqlConnection(zConnectionString);
            zConnect.Open();

            try
            {
                SqlCommand zCommand = new SqlCommand(zSQL, zConnect);
                zCommand.CommandType = CommandType.Text;
                zCommand.Parameters.Add("@MemberID", SqlDbType.NVarChar).Value = MemberID;
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
}
