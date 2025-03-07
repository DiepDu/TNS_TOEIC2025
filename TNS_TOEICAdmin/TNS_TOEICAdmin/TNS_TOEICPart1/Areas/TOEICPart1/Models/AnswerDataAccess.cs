using Microsoft.Data.SqlClient;
using System;
using System.Data;

namespace TNS_TOEICPart1.Areas.TOEICPart1.Models
{
    public class AnswerDataAccess
    {
        public class Part1_Answer_Info
        {
            #region [Field Name]
            private string _QuestionKey = "";
            private string _AnswerKey = "";
            private string _AnswerText = "";
            private bool _AnswerCorrect;
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

            #region [Constructor]
            public Part1_Answer_Info()
            {
                Guid zNewID = Guid.NewGuid();
                _AnswerKey = zNewID.ToString();
                _IsNewRecord = true;
                _Status = "OK";
            }

            public Part1_Answer_Info(string AnswerKey)
            {
                if (string.IsNullOrEmpty(AnswerKey?.Trim()))
                {
                    Guid zNewID = Guid.NewGuid();
                    _AnswerKey = zNewID.ToString();
                    _IsNewRecord = true;
                    _Status = "OK";
                    return;
                }

                string zSQL = "SELECT * FROM [dbo].[TEC_Part1_Answer] WHERE AnswerKey = @AnswerKey AND RecordStatus != 99";
                string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
                using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                {
                    zConnect.Open();
                    try
                    {
                        using (SqlCommand zCommand = new SqlCommand(zSQL, zConnect))
                        {
                            zCommand.CommandType = CommandType.Text;
                            zCommand.Parameters.Add("@AnswerKey", SqlDbType.UniqueIdentifier).Value = new Guid(AnswerKey);
                            using (SqlDataReader zReader = zCommand.ExecuteReader())
                            {
                                if (zReader.HasRows)
                                {
                                    zReader.Read();
                                    _QuestionKey = zReader["QuestionKey"].ToString();
                                    _AnswerKey = zReader["AnswerKey"].ToString();
                                    _AnswerText = zReader["AnswerText"].ToString();
                                    _AnswerCorrect = (bool)zReader["AnswerCorrect"];
                                    _RecordStatus = int.Parse(zReader["RecordStatus"].ToString());
                                    if (zReader["CreatedOn"] != DBNull.Value) _CreatedOn = (DateTime)zReader["CreatedOn"];
                                    _CreatedBy = zReader["CreatedBy"].ToString();
                                    _CreatedName = zReader["CreatedName"].ToString();
                                    if (zReader["ModifiedOn"] != DBNull.Value) _ModifiedOn = (DateTime)zReader["ModifiedOn"];
                                    _ModifiedBy = zReader["ModifiedBy"].ToString();
                                    _ModifiedName = zReader["ModifiedName"].ToString();
                                    _IsNewRecord = false;
                                    _Status = "OK";
                                    _Message = "Record found!";
                                }
                                else
                                {
                                    _Status = "ERROR";
                                    _Message = "No record found!";
                                }
                            }
                        }
                    }
                    catch (Exception Err)
                    {
                        _Status = "ERROR";
                        _Message = Err.ToString();
                    }
                }
            }
            #endregion

            #region [Properties]
            public string QuestionKey { get => _QuestionKey; set => _QuestionKey = value; }
            public string AnswerKey { get => _AnswerKey; set => _AnswerKey = value; }
            public string AnswerText { get => _AnswerText; set => _AnswerText = value; }
            public bool AnswerCorrect { get => _AnswerCorrect; set => _AnswerCorrect = value; }
            public int RecordStatus { get => _RecordStatus; set => _RecordStatus = value; }
            public DateTime? CreatedOn { get => _CreatedOn; set => _CreatedOn = value; }
            public string CreatedBy { get => _CreatedBy; set => _CreatedBy = value; }
            public string CreatedName { get => _CreatedName; set => _CreatedName = value; }
            public DateTime? ModifiedOn { get => _ModifiedOn; set => _ModifiedOn = value; }
            public string ModifiedBy { get => _ModifiedBy; set => _ModifiedBy = value; }
            public string ModifiedName { get => _ModifiedName; set => _ModifiedName = value; }
            public bool IsNewRecord { get => _IsNewRecord; }
            public string Status { get => _Status; }
            public string Message { get => _Message; set => _Message = value; }
            #endregion

            #region [CRUD Methods]
            public void Create()
            {
                if (string.IsNullOrEmpty(_QuestionKey) || !Guid.TryParse(_QuestionKey, out _))
                {
                    _Status = "ERROR";
                    _Message = "Invalid QuestionKey format: " + _QuestionKey;
                    return;
                }

                string zSQL = @"INSERT INTO [dbo].[TEC_Part1_Answer] (
                    QuestionKey, AnswerKey, AnswerText, AnswerCorrect, RecordStatus, CreatedBy, CreatedName, ModifiedBy, ModifiedName
                ) VALUES (
                    @QuestionKey, @AnswerKey, @AnswerText, @AnswerCorrect, @RecordStatus, @CreatedBy, @CreatedName, @ModifiedBy, @ModifiedName
                )";
                string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
                using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                {
                    zConnect.Open();
                    try
                    {
                        using (SqlCommand zCommand = new SqlCommand(zSQL, zConnect))
                        {
                            zCommand.CommandType = CommandType.Text;
                            zCommand.Parameters.Add("@QuestionKey", SqlDbType.UniqueIdentifier).Value = new Guid(_QuestionKey);
                            zCommand.Parameters.Add("@AnswerKey", SqlDbType.UniqueIdentifier).Value = new Guid(_AnswerKey);
                            zCommand.Parameters.Add("@AnswerText", SqlDbType.NVarChar).Value = _AnswerText ?? (object)DBNull.Value;
                            zCommand.Parameters.Add("@AnswerCorrect", SqlDbType.Bit).Value = _AnswerCorrect;
                            zCommand.Parameters.Add("@RecordStatus", SqlDbType.Int).Value = _RecordStatus;
                            zCommand.Parameters.Add("@CreatedBy", SqlDbType.UniqueIdentifier).Value = string.IsNullOrEmpty(_CreatedBy) ? DBNull.Value : new Guid(_CreatedBy);
                            zCommand.Parameters.Add("@CreatedName", SqlDbType.NVarChar).Value = _CreatedName ?? (object)DBNull.Value;
                            zCommand.Parameters.Add("@ModifiedBy", SqlDbType.UniqueIdentifier).Value = string.IsNullOrEmpty(_ModifiedBy) ? DBNull.Value : new Guid(_ModifiedBy);
                            zCommand.Parameters.Add("@ModifiedName", SqlDbType.NVarChar).Value = _ModifiedName ?? (object)DBNull.Value;
                            zCommand.ExecuteNonQuery();
                            _Status = "OK";
                            _Message = "Answer created successfully";
                        }
                    }
                    catch (Exception Err)
                    {
                        _Status = "ERROR";
                        _Message = Err.ToString();
                    }
                }
            }

            public void Update()
            {
                if (string.IsNullOrEmpty(_QuestionKey) || !Guid.TryParse(_QuestionKey, out _) || !Guid.TryParse(_AnswerKey, out _))
                {
                    _Status = "ERROR";
                    _Message = "Invalid QuestionKey or AnswerKey format";
                    return;
                }

                string zSQL = @"UPDATE [dbo].[TEC_Part1_Answer] SET 
                    AnswerText = @AnswerText, 
                    AnswerCorrect = @AnswerCorrect, 
                    RecordStatus = @RecordStatus, 
                    ModifiedOn = GETDATE(), 
                    ModifiedBy = @ModifiedBy, 
                    ModifiedName = @ModifiedName 
                    WHERE AnswerKey = @AnswerKey";
                string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
                using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                {
                    zConnect.Open();
                    try
                    {
                        using (SqlCommand zCommand = new SqlCommand(zSQL, zConnect))
                        {
                            zCommand.CommandType = CommandType.Text;
                            zCommand.Parameters.Add("@AnswerKey", SqlDbType.UniqueIdentifier).Value = new Guid(_AnswerKey);
                            zCommand.Parameters.Add("@AnswerText", SqlDbType.NVarChar).Value = _AnswerText ?? (object)DBNull.Value;
                            zCommand.Parameters.Add("@AnswerCorrect", SqlDbType.Bit).Value = _AnswerCorrect;
                            zCommand.Parameters.Add("@RecordStatus", SqlDbType.Int).Value = _RecordStatus;
                            zCommand.Parameters.Add("@ModifiedBy", SqlDbType.UniqueIdentifier).Value = string.IsNullOrEmpty(_ModifiedBy) ? DBNull.Value : new Guid(_ModifiedBy);
                            zCommand.Parameters.Add("@ModifiedName", SqlDbType.NVarChar).Value = _ModifiedName ?? (object)DBNull.Value;
                            zCommand.ExecuteNonQuery();
                            _Status = "OK";
                            _Message = "Answer updated successfully";
                        }
                    }
                    catch (Exception Err)
                    {
                        _Status = "ERROR";
                        _Message = Err.ToString();
                    }
                }
            }

            public void Delete()
            {
                if (string.IsNullOrEmpty(_AnswerKey) || !Guid.TryParse(_AnswerKey, out _))
                {
                    _Status = "ERROR";
                    _Message = "Invalid AnswerKey format";
                    return;
                }

                string zSQL = "UPDATE [dbo].[TEC_Part1_Answer] SET RecordStatus = 99 WHERE AnswerKey = @AnswerKey";
                string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
                using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                {
                    zConnect.Open();
                    try
                    {
                        using (SqlCommand zCommand = new SqlCommand(zSQL, zConnect))
                        {
                            zCommand.Parameters.Add("@AnswerKey", SqlDbType.UniqueIdentifier).Value = new Guid(_AnswerKey);
                            zCommand.ExecuteNonQuery();
                            _Status = "OK";
                            _Message = "Answer deleted successfully";
                        }
                    }
                    catch (Exception Err)
                    {
                        _Status = "ERROR";
                        _Message = Err.ToString();
                    }
                }
            }
            #endregion
        }
    }
}