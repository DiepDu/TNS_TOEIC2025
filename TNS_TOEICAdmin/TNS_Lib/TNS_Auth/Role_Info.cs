using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TNS.Auth
{
    public class Role_Info
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string ID{ get; set; }
        public bool IsCreate { get; set; }
        public bool IsRead { get; set; }
        public bool IsUpdate { get; set; }
        public bool IsDelete { get; set; }
        public bool IsApproval { get; set; }
        public bool RecordExist { get; set; }
        private string Message { get; set; }

        public Role_Info() { }   
        public Role_Info(string userKey, string RoleID)
        {
            if (RoleID == "Full")
            {
                IsRead = true;
                IsCreate = true;
                IsUpdate = true;
                IsDelete = true;
                IsApproval = true;
            }
            else
            {
                string zSQL = "SELECT A.*,B.RoleName FROM [dbo].[SYS_Users_Roles] A "
                            + "LEFT JOIN [dbo].[SYS_Roles] B ON A.RoleKey = B.RoleKey "
                            + "WHERE B.RoleID = @RoleID AND UserKey = @UserKey "
                            + "ORDER BY B.Rank";
                string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
                SqlConnection zConnect = new SqlConnection(zConnectionString);
                zConnect.Open();
                try
                {
                    SqlCommand zCommand = new SqlCommand(zSQL, zConnect);
                    zCommand.CommandType = CommandType.Text;
                    zCommand.Parameters.Add("@RoleID", SqlDbType.NVarChar).Value = RoleID;
                    zCommand.Parameters.Add("@UserKey", SqlDbType.UniqueIdentifier).Value = new Guid(userKey);
                    SqlDataReader zReader = zCommand.ExecuteReader();
                    if (zReader.HasRows)
                    {
                        zReader.Read();
                        Key = zReader["RoleKey"].ToString();
                        Name = zReader["RoleName"].ToString();
                        if (zReader["RoleRead"] == null)
                            IsRead = false;
                        else
                            IsRead = (bool)zReader["RoleRead"];

                        if (zReader["RoleAdd"] == null)
                            IsCreate = false;
                        else
                            IsCreate = (bool)zReader["RoleAdd"];

                        if (zReader["RoleEdit"] == null)
                            IsUpdate = false;
                        else
                            IsUpdate = (bool)zReader["RoleEdit"];

                        if (zReader["RoleDel"] == null)
                            IsDelete = false;
                        else
                            IsDelete = (bool)zReader["RoleDel"];

                        if (zReader["RoleApproval"] == null)
                            IsApproval = false;
                        else
                            IsApproval = (bool)zReader["RoleApproval"];
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
            }
        }
       
        public string GetCode()
        {
            if (Message.Length >= 3)
                return Message.Substring(0, 3);
            else
                return "";
        }

    }
}
