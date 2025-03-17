using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TNS_TOEICPart4.Areas.TOEICPart4.Models
{
    public class QuestionSubListAccessData
    {
        public static JsonResult GetList(string QuestionKey)
        {
            string zMessage = "";
            string zSQL = @"SELECT QuestionKey, QuestionText, Ranking";
            zSQL += " FROM [dbo].[TEC_Part4_Question] ";
            zSQL += " WHERE RecordStatus != 99 AND Parent = @QuestionKey";
            zSQL += " ORDER BY Ranking ";
            DataTable zTable = new DataTable();
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            try
            {
                SqlConnection zConnect = new SqlConnection(zConnectionString);
                zConnect.Open();
                SqlCommand zCommand = new SqlCommand(zSQL, zConnect);
                zCommand.CommandType = CommandType.Text;
                zCommand.Parameters.Add("@QuestionKey", SqlDbType.NVarChar).Value = QuestionKey;
                SqlDataAdapter zAdapter = new SqlDataAdapter(zCommand);
                zAdapter.Fill(zTable);
                zCommand.Dispose();
                zConnect.Close();
            }
            catch (Exception ex)
            {
                zMessage = ex.ToString();
            }
            var zDataList = zTable.AsEnumerable().Select(row => zTable.Columns.Cast<DataColumn>()
             .ToDictionary(col => col.ColumnName, col => row[col])).ToList();

            return new JsonResult(zDataList);

        }
    }
}
