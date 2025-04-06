using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TNS_Auth
{
    public class CheckUserLogin
    {
        private string _UserKey;
        private string _UserName;
        private string _EmployeeKey;

        private string _Message;
        private bool _Successed;
        public CheckUserLogin(string userName, string password)
        {
            DataRow zRow = TNS_Auth.Securiry.UserNameLogin(userName, out _Message);
            if (zRow != null)
            {
                _UserName = userName;
                TNS_Auth.Securiry.UpdateLastLogin(userName);
                if (TNS_Auth.MyCryptography.VerifyHash(password, zRow["Password"].ToString()))
                {
                    _UserKey = zRow["UserKey"].ToString();
                    _EmployeeKey = zRow["EmployeeKey"].ToString();
                    _Successed = true;

                }
                else
                {
                    _Message = "Nhập sai mật khẩu ! ";
                    TNS_Auth.Securiry.UpdateFailedPass(_UserName);
                }
            }
            else
            {
                if (_Message == "")
                    _Message = "Không có tài khoản này !";
            }

        }


        #region [ Properties ]
        public string UserKey { get { return _UserKey; } }
        public string UserName { get { return _UserName; } }
        public string EmployeeKey { get { return _EmployeeKey; } }
        public string Message { get { return _Message; } }
        public bool Successed { get { return _Successed; } }
        #endregion
    }
}
