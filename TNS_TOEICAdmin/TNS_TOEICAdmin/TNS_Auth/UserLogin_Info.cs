using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace TNS_Auth
{
    public class UserLogin_Info
    {
        public string UserKey { get; private set; }
        public string UserName { get; private set; }
        public Employee_Record Employee { get; private set; }
        public Role_Info Role { get; private set; }
        public UserLogin_Info(ClaimsPrincipal UserCookie)
        {

            UserKey = UserCookie.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            UserName = UserCookie.Claims.FirstOrDefault(c => c.Type == "UserName")?.Value;
            string zEmployeeKey = UserCookie.Claims.FirstOrDefault(c => c.Type == "EmployeeKey")?.Value;
            Employee = new Employee_Record(zEmployeeKey);
        }
        public void GetRole(string RoleID)
        {
            Role = new TNS_Auth.Role_Info(UserKey, RoleID);
        }
    }
}
