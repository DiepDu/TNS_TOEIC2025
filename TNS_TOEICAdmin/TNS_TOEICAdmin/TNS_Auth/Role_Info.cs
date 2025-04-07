using Microsoft.Data.SqlClient;
using System;
using System.Data;

namespace TNS_Auth
{
    public class Role_Info
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string ID { get; set; }
        public bool IsCreate { get; set; } = false; // Mặc định false
        public bool IsRead { get; set; } = false;   // Mặc định false
        public bool IsUpdate { get; set; } = false; // Mặc định false
        public bool IsDelete { get; set; } = false; // Mặc định false
        public bool IsApproval { get; set; } = false; // Mặc định false
        public bool RecordExist { get; set; }
        private string Message { get; set; } = ""; // Mặc định rỗng

        public Role_Info() { }

        public Role_Info(string userKey, string RoleID)
        {
            string zSQL = "SELECT A.*, B.RoleName FROM [dbo].[SYS_Users_Roles] A "
                        + "LEFT JOIN [dbo].[SYS_Roles] B ON A.RoleKey = B.RoleKey "
                        + "WHERE B.RoleID = @RoleID AND UserKey = @UserKey "
                        + "ORDER BY B.Rank";
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            using (SqlConnection zConnect = new SqlConnection(zConnectionString))
            {
                zConnect.Open();
                try
                {
                    SqlCommand zCommand = new SqlCommand(zSQL, zConnect);
                    zCommand.Parameters.Add("@RoleID", SqlDbType.NVarChar).Value = RoleID;
                    zCommand.Parameters.Add("@UserKey", SqlDbType.UniqueIdentifier).Value = new Guid(userKey);
                    SqlDataReader zReader = zCommand.ExecuteReader();
                    if (zReader.HasRows)
                    {
                        zReader.Read();
                        Key = zReader["RoleKey"].ToString();
                        Name = zReader["RoleName"].ToString();
                        IsRead = zReader["RoleRead"] == DBNull.Value ? false : (bool)zReader["RoleRead"];
                        IsCreate = zReader["RoleAdd"] == DBNull.Value ? false : (bool)zReader["RoleAdd"];
                        IsUpdate = zReader["RoleEdit"] == DBNull.Value ? false : (bool)zReader["RoleEdit"];
                        IsDelete = zReader["RoleDel"] == DBNull.Value ? false : (bool)zReader["RoleDel"];
                        IsApproval = zReader["RoleApproval"] == DBNull.Value ? false : (bool)zReader["RoleApproval"];
                        RecordExist = true;
                        Message = "200 OK";

                        // Nếu là Full và có trong DB, override tất cả quyền
                        if (RoleID == "Full")
                        {
                            IsRead = true;
                            IsCreate = true;
                            IsUpdate = true;
                            IsDelete = true;
                            IsApproval = true;
                        }
                    }
                    else
                    {
                        RecordExist = false;
                        Message = "404 Not Found";
                    }
                    zReader.Close();
                }
                catch (Exception Err)
                {
                    Message = "501 " + Err.ToString();
                }
            }
        }

        public string GetCode()
        {
            if (string.IsNullOrEmpty(Message) || Message.Length < 3)
                return "";
            return Message.Substring(0, 3);
        }
    }
}