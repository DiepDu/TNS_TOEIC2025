using System;

namespace TNS.DBConnection
{
    public class Connecting
    {
        // public static string SQL_MainDatabase = @"Data Source=171.235.24.157;DataBase=TNS_Education;user=education;Password=tns12345Aa@;";
        //public static string SQL_MainDatabase = @"Data Source=171.235.24.157;DataBase=TNS_Toeic;user=TOEIC;Password=Tns@123;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        //public static string SQL_MainDatabase = @"Data Source=171.235.24.157;DataBase=Banking;user=KPI;Password=Tns6868#;";
        //public static string SQL_MainDatabase = @"Data Source=171.235.24.157;DataBase=TNS_Enterprice;user=hht;Password=123456Aa@;";
        public static string SQL_MainDatabase = @"Server=tcp:toeic.database.windows.net,1433;Initial Catalog=TNS_Toeic;Persist Security Info=False;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=""Active Directory Default"";";
        //public static string SQL_MainDatabase = @"Server=tcp:toeic.database.windows.net,1433;Initial Catalog=TNS_TOEIC;Persist Security Info=False;User ID=HieuNN;Password=MatKhauCuaHieu123!;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
    }
}