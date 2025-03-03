using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace TNS.Member
{
    public class MemberLogin_Info
    {
        public string MemberKey { get; private set; }
        public string MemberName { get; private set; }
        public string Avatar { get; private set; }
        public MemberLogin_Info(ClaimsPrincipal MemberCookie)
        {

            MemberKey = MemberCookie.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            MemberName = MemberCookie.Claims.FirstOrDefault(c => c.Type == "MemberName")?.Value;
            Avatar = MemberCookie.Claims.FirstOrDefault(c => c.Type == "Avatar")?.Value;

        }
       
    }
}
