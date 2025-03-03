using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TNS.Auth
{
    public class AccessData
    {

     
    }

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
            HashAlgorithm mHash = HashAlgorithm.Create("SHA1");

            byte[] pwordData = Encoding.Default.GetBytes(nPass);

            byte[] nHash = mHash.ComputeHash(pwordData);

            return System.Convert.ToBase64String(nHash);
        }

        public static Boolean VerifyHash(string NewPass, string OldPass)
        {
            string HashNewPass = HashPass(NewPass);
            return (OldPass == HashNewPass);
        }
    }
}
