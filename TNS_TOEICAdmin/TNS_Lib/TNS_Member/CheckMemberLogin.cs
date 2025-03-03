using System.Data;

namespace TNS.Member
{
    public class CheckMemberLogin
    {
        private string _MemberKey;
        private string _MemberID;
        private string _MemberName;
        private string _Avatar;
        private string _Message;
        private bool _Successed;
        public CheckMemberLogin(string ID, string password)
        {
            DataRow zRow = Member.Securiry.MemberLogin(ID, out _Message);
            if (zRow != null)
            {
                _MemberID = ID ;
                Member.Securiry.UpdateLastLogin(_MemberID);
                if (Member.MyCryptography.VerifyHash(password, zRow["Password"].ToString()))
                {
                    _MemberKey = zRow["MemberKey"].ToString();
                    _MemberName = zRow["MemberName"].ToString();
                    _Avatar = zRow["Avatar"].ToString();
                    _Successed = true;

                }
                else
                {
                    _Message = "Nhập sai mật khẩu ! ";
                    Member.Securiry.UpdateFailedPass(_MemberID);
                }
            }
            else
            {
                if (_Message == "")
                    _Message = "Không có tài khoản này !";
            }

        }


        #region [ Properties ]
        public string MemberKey { get { return _MemberKey; } }
        public string MemberName { get { return _MemberName; } }
        public string Avatar { get { return _Avatar; } }
        public string Message { get { return _Message; } }
        public bool Successed { get { return _Successed; } }
        #endregion
    }
}
