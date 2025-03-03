using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
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
                        + "WHERE MemberID = @MemberID ";
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
            using (SHA1 mHash = SHA1.Create())
            {
                byte[] pwordData = Encoding.UTF8.GetBytes(nPass.Trim());
                byte[] nHash = mHash.ComputeHash(pwordData);

                // Trả về dạng Hexadecimal
                return BitConverter.ToString(nHash).Replace("-", "").ToLower();
            }
        }



        public static Boolean VerifyHash(string NewPass, string OldPass)
        {
            string HashNewPass = HashPass(NewPass);
            return (OldPass == HashNewPass);
        }
    }
}
