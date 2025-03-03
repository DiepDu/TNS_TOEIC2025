using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Data.SqlClient;

namespace TNS.Auth
{
    public class Employee_Record
    {
        #region [ Field Name ]
        private string _EmployeeKey = "";
        private string _EmployeeID = "";
        private string _LastName = "";
        private string _FirstName = "";
        private string _DepartmentKey = "";
        private string _DepartmentName = "";
        private string _Message = "";
        #endregion

        #region [ Constructor Get Information ]
        public Employee_Record()
        {

        }
        public Employee_Record(string EmployeeKey)
        {
            string zSQL = "SELECT * FROM [dbo].[HRM_Employee] WHERE EmployeeKey = @EmployeeKey AND RecordStatus != 99 ";
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            SqlConnection zConnect = new SqlConnection(zConnectionString);
            zConnect.Open();
            try
            {
                SqlCommand zCommand = new SqlCommand(zSQL, zConnect);
                zCommand.CommandType = CommandType.Text;
                zCommand.Parameters.Add("@EmployeeKey", SqlDbType.UniqueIdentifier).Value = new Guid(EmployeeKey);
                SqlDataReader zReader = zCommand.ExecuteReader();
                if (zReader.HasRows)
                {
                    zReader.Read();
                    _EmployeeKey = zReader["EmployeeKey"].ToString();
                    _EmployeeID = zReader["EmployeeID"].ToString();
                    _LastName = zReader["LastName"].ToString();
                    _FirstName = zReader["FirstName"].ToString();
                    _DepartmentKey = zReader["DepartmentKey"].ToString();
                    _DepartmentName = zReader["DepartmentName"].ToString();
                    _Message = "200 OK";
                }
                else
                {
                    _Message = "404 Not Found";
                }
                zReader.Close();
                zCommand.Dispose();
            }
            catch (Exception Err)
            {
                _Message = "501 " + Err.ToString();
            }
            finally
            {
                zConnect.Close();
            }
        }
      
        #endregion

        #region [ Properties ]
        public string Key
        {
            get { return _EmployeeKey; }
            set { _EmployeeKey = value; }
        }
        public string ID
        {
            get { return _EmployeeID; }
            set { _EmployeeID = value; }
        }

        public string Name
        {
            get { return _LastName + " " + _FirstName ; }
        }
        public string LastName
        {
            get { return _LastName; }
            set { _LastName = value; }
        }
        public string FirstName
        {
            get { return _FirstName; }
            set { _FirstName = value; }
        }
        public string DepartmentKey
        {
            get { return _DepartmentKey; }
            set { _DepartmentKey = value; }
        }
        public string DepartmentName
        {
            get { return _DepartmentName; }
            set { _DepartmentName = value; }
        }
       
        public string Code
        {
            get
            {
                if (_Message.Length >= 3)
                    return _Message.Substring(0, 3);
                else return "";
            }
        }
        public string Message
        {
            get { return _Message; }
            set { _Message = value; }
        }
        #endregion

        
    }
}
