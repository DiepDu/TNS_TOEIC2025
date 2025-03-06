using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TNS_TOEICPart1.Areas.TOEICPart1.Models
{
    public class QuestionAccessData
    {
        public class Part1_Question_Info
        {

            #region [ Field Name ]
            private string _QuestionKey = "";
            private string _QuestionText = "";
            private string _QuestionImage = "";
            private string _QuestionVoice = "";
            private int _SkillLevel = 0;
            private int _AmountAccess = 0;
            private string _Parent = "";
            private bool _Publish;
            private int _RecordStatus = 0;
            private DateTime? _CreatedOn = null;
            private string _CreatedBy = "";
            private string _CreatedName = "";
            private DateTime? _ModifiedOn = null;
            private string _ModifiedBy = "";
            private string _ModifiedName = "";
            private string _Message = "";
            private bool _IsNewRecord = true;
            private string _Status = "";
            #endregion

            #region [ Constructor Get Information ]
            public Part1_Question_Info()
            {
                Guid zNewID = Guid.NewGuid();
                _QuestionKey = zNewID.ToString();
                _IsNewRecord = true;
            }
            public Part1_Question_Info(string QuestionKey)
            {
                if (QuestionKey.Trim().Length == 0)
                {
                    Guid zNewID = Guid.NewGuid();
                    _QuestionKey = zNewID.ToString();
                    _IsNewRecord = true;
                    _Status = "OK";
                    return;
                }

                string zSQL = "SELECT * FROM [dbo].[TEC_Part1_Question] WHERE QuestionKey = @QuestionKey AND RecordStatus != 99 ";
                string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
                SqlConnection zConnect = new SqlConnection(zConnectionString);
                zConnect.Open();
                try
                {
                    SqlCommand zCommand = new SqlCommand(zSQL, zConnect);
                    zCommand.CommandType = CommandType.Text;
                    zCommand.Parameters.Add("@QuestionKey", SqlDbType.UniqueIdentifier).Value = new Guid(QuestionKey);
                    SqlDataReader zReader = zCommand.ExecuteReader();
                    if (zReader.HasRows)
                    {
                        zReader.Read();
                        _QuestionKey = zReader["QuestionKey"].ToString();
                        _QuestionText = zReader["QuestionText"].ToString();
                        _QuestionImage = zReader["QuestionImage"].ToString();
                        _QuestionVoice = zReader["QuestionVoice"].ToString();
                        _SkillLevel = int.Parse(zReader["SkillLevel"].ToString());
                        _AmountAccess = int.Parse(zReader["AmountAccess"].ToString());
                        _Parent = zReader["Parent"].ToString();
                        _Publish = (bool)zReader["Publish"];
                        _RecordStatus = int.Parse(zReader["RecordStatus"].ToString());
                        if (zReader["CreatedOn"] != DBNull.Value)
                            _CreatedOn = (DateTime)zReader["CreatedOn"];
                        _CreatedBy = zReader["CreatedBy"].ToString();
                        _CreatedName = zReader["CreatedName"].ToString();
                        if (zReader["ModifiedOn"] != DBNull.Value)
                            _ModifiedOn = (DateTime)zReader["ModifiedOn"];
                        _ModifiedBy = zReader["ModifiedBy"].ToString();
                        _ModifiedName = zReader["ModifiedName"].ToString();
                        _IsNewRecord = false;
                        _Status = "OK";// Record found
                        _Message = "Record found !";
                    }
                    else
                    {
                        _Status = "ERROR";
                        _Message = "No record found !";
                    }
                    zReader.Close();
                    zCommand.Dispose();
                }
                catch (Exception Err)
                {
                    _Status = "ERROR";  // Error when connect to SQL Server
                    _Message = Err.ToString();
                }
                finally
                {
                    zConnect.Close();
                }
            }

            #endregion

            #region [ Properties ]
            public string QuestionKey
            {
                get { return _QuestionKey; }
                set { _QuestionKey = value; }
            }
            public string QuestionText
            {
                get { return _QuestionText; }
                set { _QuestionText = value; }
            }
            public string QuestionImage
            {
                get { return _QuestionImage; }
                set { _QuestionImage = value; }
            }
            public string QuestionVoice
            {
                get { return _QuestionVoice; }
                set { _QuestionVoice = value; }
            }
            public int SkillLevel
            {
                get { return _SkillLevel; }
                set { _SkillLevel = value; }
            }
            public int AmountAccess
            {
                get { return _AmountAccess; }
                set { _AmountAccess = value; }
            }
            public string Parent
            {
                get { return _Parent; }
                set { _Parent = value; }
            }
            public bool Publish
            {
                get { return _Publish; }
                set { _Publish = value; }
            }
            public int RecordStatus
            {
                get { return _RecordStatus; }
                set { _RecordStatus = value; }
            }
            public DateTime? CreatedOn
            {
                get { return _CreatedOn; }
                set { _CreatedOn = value; }
            }
            public string CreatedBy
            {
                get { return _CreatedBy; }
                set { _CreatedBy = value; }
            }
            public string CreatedName
            {
                get { return _CreatedName; }
                set { _CreatedName = value; }
            }
            public DateTime? ModifiedOn
            {
                get { return _ModifiedOn; }
                set { _ModifiedOn = value; }
            }
            public string ModifiedBy
            {
                get { return _ModifiedBy; }
                set { _ModifiedBy = value; }
            }
            public string ModifiedName
            {
                get { return _ModifiedName; }
                set { _ModifiedName = value; }
            }

            public bool IsNewRecord
            {
                get { return _IsNewRecord; }
            }
            public string Status
            {
                get { return _Status; }
            }
            public string Message
            {
                get { return _Message; }
                set { _Message = value; }
            }
            #endregion

            #region [ Constructor Update Information ]
            public string Create()
            {
                //---------- String SQL Access Database ---------------
                string zSQL = "INSERT INTO [dbo].[TEC_Part1_Question] ("
            + " QuestionKey ,QuestionImage,QuestionText ,QuestionVoice ,SkillLevel  ,Parent ,Publish ,RecordStatus ,CreatedBy ,CreatedName ,ModifiedBy ,ModifiedName ) "
             + " VALUES ( "
             + "@QuestionKey ,@QuestionImage,@QuestionText ,@QuestionVoice ,@SkillLevel ,@Parent ,@Publish ,@RecordStatus ,@CreatedBy ,@CreatedName ,@ModifiedBy ,@ModifiedName ) ";
                string zResult = "";
                string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
                SqlConnection zConnect = new SqlConnection(zConnectionString);
                zConnect.Open();
                try
                {
                    SqlCommand zCommand = new SqlCommand(zSQL, zConnect);
                    zCommand.CommandType = CommandType.Text;
                    if (_QuestionKey.Length == 36)
                        zCommand.Parameters.Add("@QuestionKey", SqlDbType.UniqueIdentifier).Value = new Guid(_QuestionKey);
                    else
                        zCommand.Parameters.Add("@QuestionKey", SqlDbType.UniqueIdentifier).Value = DBNull.Value;
                    zCommand.Parameters.Add("@QuestionText", SqlDbType.NVarChar).Value = _QuestionText;
                    zCommand.Parameters.Add("@QuestionImage", SqlDbType.NVarChar).Value = _QuestionImage;
                    zCommand.Parameters.Add("@QuestionVoice", SqlDbType.NVarChar).Value = _QuestionVoice;
                    zCommand.Parameters.Add("@SkillLevel", SqlDbType.Int).Value = _SkillLevel;
                    zCommand.Parameters.Add("@AmountAccess", SqlDbType.Int).Value = _AmountAccess;

                    if (_Parent.Length == 36)
                        zCommand.Parameters.Add("@Parent", SqlDbType.UniqueIdentifier).Value = new Guid(_Parent);
                    else
                        zCommand.Parameters.Add("@Parent", SqlDbType.UniqueIdentifier).Value = DBNull.Value;
                    zCommand.Parameters.Add("@Publish", SqlDbType.Bit).Value = _Publish;
                    zCommand.Parameters.Add("@RecordStatus", SqlDbType.Int).Value = _RecordStatus;
                    if (_CreatedBy.Length == 36)
                        zCommand.Parameters.Add("@CreatedBy", SqlDbType.UniqueIdentifier).Value = new Guid(_CreatedBy);
                    else
                        zCommand.Parameters.Add("@CreatedBy", SqlDbType.UniqueIdentifier).Value = DBNull.Value;
                    zCommand.Parameters.Add("@CreatedName", SqlDbType.NVarChar).Value = _CreatedName;
                    if (_ModifiedBy.Length == 36)
                        zCommand.Parameters.Add("@ModifiedBy", SqlDbType.UniqueIdentifier).Value = new Guid(_ModifiedBy);
                    else
                        zCommand.Parameters.Add("@ModifiedBy", SqlDbType.UniqueIdentifier).Value = DBNull.Value;
                    zCommand.Parameters.Add("@ModifiedName", SqlDbType.NVarChar).Value = _ModifiedName;
                    zResult = zCommand.ExecuteNonQuery().ToString();
                    zCommand.Dispose();
                    _Status = "OK";
                    _Message = "201 Created";
                }
                catch (Exception Err)
                {
                    _Message = "501 " + Err.ToString();
                }
                finally
                {
                    zConnect.Close();
                }
                return zResult;
            }


            public string Update()
            {
                string zSQL = "UPDATE [dbo].[TEC_Part1_Question] SET " +
                              "QuestionText = @QuestionText, " +
                              "QuestionImage = @QuestionImage, " +
                              "QuestionVoice = @QuestionVoice, " +
                              "SkillLevel = @SkillLevel, " +
                              "Parent = @Parent, " +
                              "Publish = @Publish, " +
                              "RecordStatus = @RecordStatus, " +
                              "ModifiedOn = GETDATE(), " +
                              "ModifiedBy = @ModifiedBy, " +
                              "ModifiedName = @ModifiedName " +
                              "WHERE QuestionKey = @QuestionKey";

                string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
                using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                {
                    zConnect.Open();
                    using (SqlCommand zCommand = new SqlCommand(zSQL, zConnect))
                    {
                        zCommand.Parameters.Add("@QuestionKey", SqlDbType.UniqueIdentifier).Value = new Guid(_QuestionKey);
                        zCommand.Parameters.Add("@QuestionText", SqlDbType.NVarChar).Value = _QuestionText ?? (object)DBNull.Value;
                        zCommand.Parameters.Add("@QuestionImage", SqlDbType.NVarChar).Value = _QuestionImage ?? (object)DBNull.Value;
                        zCommand.Parameters.Add("@QuestionVoice", SqlDbType.NVarChar).Value = _QuestionVoice ?? (object)DBNull.Value;
                        zCommand.Parameters.Add("@SkillLevel", SqlDbType.Int).Value = _SkillLevel;
                        zCommand.Parameters.Add("@Parent", SqlDbType.UniqueIdentifier).Value = string.IsNullOrEmpty(_Parent) ? (object)DBNull.Value : new Guid(_Parent);
                        zCommand.Parameters.Add("@Publish", SqlDbType.Bit).Value = _Publish;
                        zCommand.Parameters.Add("@RecordStatus", SqlDbType.Int).Value = _RecordStatus;
                        zCommand.Parameters.Add("@ModifiedBy", SqlDbType.UniqueIdentifier).Value = string.IsNullOrEmpty(_ModifiedBy) ? (object)DBNull.Value : new Guid(_ModifiedBy);
                        zCommand.Parameters.Add("@ModifiedName", SqlDbType.NVarChar).Value = _ModifiedName ?? (object)DBNull.Value;

                        int rowsAffected = zCommand.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            _Status = "OK";
                            _Message = "Cập nhật thành công";
                        }
                        else
                        {
                            _Status = "ERROR";
                            _Message = "Không tìm thấy bản ghi để cập nhật.";
                        }
                        return rowsAffected.ToString();
                    }
                }
            }


            public string Delete()
            {
                string zResult = "";
                //---------- String SQL Access Database ---------------
                string zSQL = "UPDATE [dbo].[TEC_Part1_Question] Set RecordStatus = 99 WHERE QuestionKey = @QuestionKey";
                string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
                SqlConnection zConnect = new SqlConnection(zConnectionString);
                zConnect.Open();
                try
                {
                    SqlCommand zCommand = new SqlCommand(zSQL, zConnect);
                    zCommand.Parameters.Add("@QuestionKey", SqlDbType.UniqueIdentifier).Value = new Guid(_QuestionKey);
                    zResult = zCommand.ExecuteNonQuery().ToString();
                    zCommand.Dispose();
                    _Status = "OK";
                }
                catch (Exception Err)
                {
                    _Status = "ERROR";
                    _Message = Err.ToString();
                }
                finally
                {
                    zConnect.Close();
                }
                return zResult;
            }
            public string Empty()
            {
                string zResult = "";
                //---------- String SQL Access Database ---------------
                string zSQL = "DELETE FROM [dbo].[TEC_Part1_Question] WHERE QuestionKey = @QuestionKey";
                string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
                SqlConnection zConnect = new SqlConnection(zConnectionString);
                zConnect.Open();
                try
                {
                    SqlCommand zCommand = new SqlCommand(zSQL, zConnect);
                    zCommand.Parameters.Add("@QuestionKey", SqlDbType.UniqueIdentifier).Value = new Guid(_QuestionKey);
                    zResult = zCommand.ExecuteNonQuery().ToString();
                    zCommand.Dispose();
                    _Status = "OK";
                }
                catch (Exception Err)
                {
                    _Status = "ERROR";
                    _Message = Err.ToString();
                }
                finally
                {
                    zConnect.Close();
                }
                return zResult;
            }
            #endregion
        }
    }
}
