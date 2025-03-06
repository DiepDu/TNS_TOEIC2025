using System.Data;
using Microsoft.Data.SqlClient;

namespace TNS_TOEICAdmin.Models
{
    public class Personal
    {
        public string UserKey { get; set; }
        public string EmployeeName { get; private set; }
        public string EmployeeKey { get; private set; }
        public string EmployeeAvatar { get; private set; }
        public int NoticeNotRead { get; private set; }
        public int TaskNotFinish { get; private set; }
        public string Message { get; private set; }

        public Personal(string userKey, string employeeKey)
        {
            Message = "";
            this.UserKey = userKey;
            this.EmployeeKey = employeeKey;
            //NoticeNotRead = GetAmountNoticeNotRead(userKey);
            // TaskNotFinish = GetAmountTaskNotFinish();
            GetEmployeeInfo(employeeKey);
        }
        private void GetEmployeeInfo(string employeeKey)
        {
            string zSQL = "SELECT LastName, FirstName, PhotoPath FROM [dbo].[HRM_Employee] WHERE EmployeeKey = @EmployeeKey AND RecordStatus != 99 ";
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            SqlConnection zConnect = new SqlConnection(zConnectionString);
            zConnect.Open();
            try
            {
                SqlCommand zCommand = new SqlCommand(zSQL, zConnect);
                zCommand.CommandType = CommandType.Text;
                zCommand.Parameters.Add("@EmployeeKey", SqlDbType.UniqueIdentifier).Value = new Guid(employeeKey);
                SqlDataReader zReader = zCommand.ExecuteReader();
                if (zReader.HasRows)
                {
                    zReader.Read();
                    EmployeeName = zReader["LastName"].ToString() + " " + zReader["FirstName"].ToString();
                    EmployeeAvatar = zReader["PhotoPath"].ToString();

                    Message = "200 OK";
                }
                else
                {
                    Message = "404 Not Found";
                }
                zReader.Close();
                zCommand.Dispose();
            }
            catch (Exception Err)
            {
                Message = "501 " + Err.ToString();
            }
            finally
            {
                zConnect.Close();
            }

            //private int GetAmountNoticeNotRead(string userKey)
            //{
            //    int zQuantity = 0;
            //    string zSQL = "SELECT COUNT(*) FROM [dbo].[HRM_Notices] "
            //                + "WHERE [dbo].[NoticeReadStatus](NoticeKey,@UserKey) = 0";
            //    string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            //    try
            //    {
            //        SqlConnection zConnect = new SqlConnection(zConnectionString);
            //        zConnect.Open();
            //        SqlCommand zCommand = new SqlCommand(zSQL, zConnect);
            //        zCommand.Parameters.Add("@UserKey", SqlDbType.UniqueIdentifier).Value = new Guid(userKey);
            //        string zResult = zCommand.ExecuteScalar().ToString();
            //        int.TryParse(zResult, out zQuantity);

            //        zCommand.Dispose();
            //        zConnect.Close();
            //    }
            //    catch (Exception ex)
            //    {
            //        string zstrMessage = ex.ToString();
            //    }

            //    return zQuantity;
            //}
            //private int GetAmountTaskNotFinish(string employeeKey)
            //{
            //    int zQuantity = 0;
            //    string zSQL = "SELECT Count(*) FROM [dbo].[CRM_Task] "
            //                + "WHERE OwnerBy = @EmployeeKey AND StatusKey <= 2 ";
            //    string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            //    try
            //    {
            //        SqlConnection zConnect = new SqlConnection(zConnectionString);
            //        zConnect.Open();
            //        SqlCommand zCommand = new SqlCommand(zSQL, zConnect);
            //        zCommand.Parameters.Add("@EmployeeKey", SqlDbType.UniqueIdentifier).Value = new Guid(employeeKey);
            //        string zResult = zCommand.ExecuteScalar().ToString();
            //        int.TryParse(zResult, out zQuantity);

            //        zCommand.Dispose();
            //        zConnect.Close();
            //    }
            //    catch (Exception ex)
            //    {
            //        string zstrMessage = ex.ToString();
            //    }

            //    return zQuantity;
            //}


        }
    }
}
