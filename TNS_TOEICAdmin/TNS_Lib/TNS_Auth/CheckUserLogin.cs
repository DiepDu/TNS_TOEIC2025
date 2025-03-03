using System.Data;

namespace TNS.Auth
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
            DataRow zRow = Auth.Securiry.UserNameLogin(userName, out _Message);
            if (zRow != null)
            {
                _UserName = userName;
                Auth.Securiry.UpdateLastLogin(userName);
                if (zRow["Password"].ToString() == Auth.MyCryptography.HashPass(password))
                {
                    _UserKey = zRow["UserKey"].ToString();
                    _EmployeeKey = zRow["EmployeeKey"].ToString();
                    _Successed = true;

                }
                else
                {
                    _Message = "Nhập sai mật khẩu ! ";
                    Auth.Securiry.UpdateFailedPass(_UserName);
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
