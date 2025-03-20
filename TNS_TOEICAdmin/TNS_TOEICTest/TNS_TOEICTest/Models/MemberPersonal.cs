using System;
using System.Security.Claims;
using TNS.Member;

namespace TNS_TOEICTest.Models
{
    public class MemberPersonal
    {
        public string MemberName { get; set; }
        public string Avatar { get; set; }
        public bool IsAuthenticated { get; set; }
        public string ErrorMessage { get; set; }

        public MemberPersonal(ClaimsPrincipal user)
        {
            try
            {
                var memberInfo = new MemberLogin_Info(user);
                if (string.IsNullOrEmpty(memberInfo.MemberKey))
                {
                    IsAuthenticated = false;
                    ErrorMessage = "User not logged in";
                }
                else
                {
                    IsAuthenticated = true;
                    MemberName = memberInfo.MemberName ?? "User";
                    Avatar = memberInfo.Avatar ?? "/images/avatar/default-avatar.jpg";
                    if (!string.IsNullOrEmpty(Avatar) && !Avatar.StartsWith("http") && !Avatar.StartsWith("/"))
                    {
                        Avatar = $"/images/avatar/{Avatar}";
                    }
                }
            }
            catch (Exception ex)
            {
                IsAuthenticated = false;
                ErrorMessage = $"Error retrieving member info: {ex.Message}";
                MemberName = "User";
                Avatar = "/images/avatar/default-avatar.jpg";
            }
        }
    }
}