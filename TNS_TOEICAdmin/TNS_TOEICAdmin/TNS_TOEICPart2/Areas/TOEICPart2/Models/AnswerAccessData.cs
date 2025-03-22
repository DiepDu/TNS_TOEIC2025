using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TNS_TOEICPart2.Areas.TOEICPart2.Models
{
    public class AnswerAccessData
    {
        public class Part2_Answer_Info
        {
            #region [Fields]
            private string _AnswerKey = "";
            private string _QuestionKey = "";
            private string _AnswerText = "";
            private bool _AnswerCorrect;
            private int _Ranking = 0;
            private string _Category = "";
            private string _GrammarTopic = "";
            private string _ErrorType = "";
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

            #region [Constructors]
            public Part2_Answer_Info()
            {
                _AnswerKey = Guid.NewGuid().ToString();
                _QuestionKey = "";
                _AnswerText = "";
                _AnswerCorrect = false;
                _Ranking = 0;
                _Category = "";
                _GrammarTopic = "";
                _ErrorType = "";
                _RecordStatus = 0;
                _CreatedOn = null;
                _CreatedBy = "";
                _CreatedName = "";
                _ModifiedOn = null;
                _ModifiedBy = "";
                _ModifiedName = "";
                _Message = "";
                _IsNewRecord = true;
                _Status = "OK";
            }

            public Part2_Answer_Info(string questionKey)
            {
                if (string.IsNullOrEmpty(questionKey) || questionKey.Length != 36)
                    throw new ArgumentException("QuestionKey must be a valid GUID");

                _AnswerKey = Guid.NewGuid().ToString();
                _QuestionKey = questionKey.Trim();
                _Ranking = 0;
                _IsNewRecord = true;
                _Status = "OK";
            }

            public Part2_Answer_Info(string answerKey, string questionKey)
            {
                if (string.IsNullOrEmpty(answerKey) || answerKey.Length != 36)
                {
                    if (!string.IsNullOrEmpty(questionKey) && questionKey.Length == 36)
                    {
                        _AnswerKey = Guid.NewGuid().ToString();
                        _QuestionKey = questionKey.Trim();
                        _Ranking = 0;
                        _IsNewRecord = true;
                        _Status = "OK";
                    }
                    else
                    {
                        _Status = "ERROR";
                        _Message = "Invalid AnswerKey or QuestionKey";
                    }
                    return;
                }

                string sql = "SELECT * FROM [dbo].[TEC_Part2_Answer] WHERE AnswerKey = @AnswerKey AND RecordStatus != 99";
                string connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@AnswerKey", Guid.Parse(answerKey));
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                reader.Read();
                                _AnswerKey = reader["AnswerKey"].ToString();
                                _QuestionKey = reader["QuestionKey"].ToString();
                                _AnswerText = reader["AnswerText"]?.ToString() ?? "";
                                _AnswerCorrect = reader["AnswerCorrect"] != DBNull.Value && (bool)reader["AnswerCorrect"];
                                _Ranking = reader["Ranking"] != DBNull.Value ? (int)reader["Ranking"] : 0;
                                _Category = reader["Category"]?.ToString() ?? "";
                                _GrammarTopic = reader["GrammarTopic"]?.ToString() ?? ""; // Lưu ý: Cột trong DB là GrammarTopic
                                _ErrorType = reader["ErrorType"]?.ToString() ?? "";
                                _RecordStatus = (int)reader["RecordStatus"];
                                _CreatedOn = reader["CreatedOn"] != DBNull.Value ? (DateTime?)reader["CreatedOn"] : null;
                                _CreatedBy = reader["CreatedBy"]?.ToString() ?? "";
                                _CreatedName = reader["CreatedName"]?.ToString() ?? "";
                                _ModifiedOn = reader["ModifiedOn"] != DBNull.Value ? (DateTime?)reader["ModifiedOn"] : null;
                                _ModifiedBy = reader["ModifiedBy"]?.ToString() ?? "";
                                _ModifiedName = reader["ModifiedName"]?.ToString() ?? "";
                                _IsNewRecord = false;
                                _Status = "OK";
                                _Message = "Record found";
                            }
                            else
                            {
                                _Status = "ERROR";
                                _Message = "No record found";
                            }
                        }
                    }
                }
            }
            #endregion

            #region [Properties]
            public string AnswerKey { get => _AnswerKey; set => _AnswerKey = value; }
            public string QuestionKey { get => _QuestionKey; set => _QuestionKey = value; }
            public string AnswerText { get => _AnswerText; set => _AnswerText = value; }
            public bool AnswerCorrect { get => _AnswerCorrect; set => _AnswerCorrect = value; }
            public int Ranking { get => _Ranking; set => _Ranking = value; }
            public string Category { get => _Category; set => _Category = value; }
            public string GrammarTopic { get => _GrammarTopic; set => _GrammarTopic = value; }
            public string ErrorType { get => _ErrorType; set => _ErrorType = value; }
            public int RecordStatus { get => _RecordStatus; set => _RecordStatus = value; }
            public DateTime? CreatedOn { get => _CreatedOn; set => _CreatedOn = value; }
            public string CreatedBy { get => _CreatedBy; set => _CreatedBy = value; }
            public string CreatedName { get => _CreatedName; set => _CreatedName = value; }
            public DateTime? ModifiedOn { get => _ModifiedOn; set => _ModifiedOn = value; }
            public string ModifiedBy { get => _ModifiedBy; set => _ModifiedBy = value; }
            public string ModifiedName { get => _ModifiedName; set => _ModifiedName = value; }
            public bool IsNewRecord { get => _IsNewRecord; set => _IsNewRecord = value; }
            public string Status { get => _Status; set => _Status = value; }
            public string Message { get => _Message; set => _Message = value; }
            #endregion

            #region [CRUD Methods]
            public void Create()
            {
                if (string.IsNullOrEmpty(_QuestionKey) || _QuestionKey.Length != 36)
                {
                    _Status = "ERROR";
                    _Message = "Invalid QuestionKey";
                    return;
                }

                string sql = @"INSERT INTO [dbo].[TEC_Part2_Answer] 
                    (QuestionKey, AnswerKey, AnswerText, AnswerCorrect,Ranking, Category, GrammarTopic, ErrorType, RecordStatus, CreatedBy, CreatedName, ModifiedBy, ModifiedName)
                    VALUES 
                    (@QuestionKey, @AnswerKey, @AnswerText, @AnswerCorrect,@Ranking, @Category, @GrammarTopic, @ErrorType, @RecordStatus, @CreatedBy, @CreatedName, @ModifiedBy, @ModifiedName)";

                string connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
                using (var conn = new SqlConnection(connectionString))
                {
                    try
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@QuestionKey", Guid.Parse(_QuestionKey));
                            cmd.Parameters.AddWithValue("@AnswerKey", Guid.Parse(_AnswerKey));
                            cmd.Parameters.AddWithValue("@AnswerText", (object)_AnswerText ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@AnswerCorrect", _AnswerCorrect);
                            cmd.Parameters.AddWithValue("@Ranking", _Ranking);
                            cmd.Parameters.AddWithValue("@Category", string.IsNullOrEmpty(_Category) ? DBNull.Value : Guid.Parse(_Category));
                            cmd.Parameters.AddWithValue("@GrammarTopic", string.IsNullOrEmpty(_GrammarTopic) ? DBNull.Value : Guid.Parse(_GrammarTopic));
                            cmd.Parameters.AddWithValue("@ErrorType", string.IsNullOrEmpty(_ErrorType) ? DBNull.Value : Guid.Parse(_ErrorType));
                            cmd.Parameters.AddWithValue("@RecordStatus", _RecordStatus);
                            cmd.Parameters.AddWithValue("@CreatedBy", string.IsNullOrEmpty(_CreatedBy) ? DBNull.Value : Guid.Parse(_CreatedBy));
                            cmd.Parameters.AddWithValue("@CreatedName", (object)_CreatedName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ModifiedBy", string.IsNullOrEmpty(_ModifiedBy) ? DBNull.Value : Guid.Parse(_ModifiedBy));
                            cmd.Parameters.AddWithValue("@ModifiedName", (object)_ModifiedName ?? DBNull.Value);

                            int rowsAffected = cmd.ExecuteNonQuery();
                            if (rowsAffected > 0)
                            {
                                _Status = "OK";
                                _Message = "Answer created successfully";
                                _IsNewRecord = false;
                            }
                            else
                            {
                                _Status = "ERROR";
                                _Message = "Failed to create answer";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _Status = "ERROR";
                        _Message = $"Error creating answer: {ex.Message}";
                    }
                }
            }

            public void Update()
            {
                if (string.IsNullOrEmpty(_AnswerKey) || _AnswerKey.Length != 36)
                {
                    _Status = "ERROR";
                    _Message = "Invalid AnswerKey";
                    return;
                }

                string sql = @"UPDATE [dbo].[TEC_Part2_Answer] 
                    SET AnswerText = @AnswerText, AnswerCorrect = @AnswerCorrect,Ranking = @Ranking, Category = @Category, GrammarTopic = @GrammarTopic, ErrorType = @ErrorType, 
                        RecordStatus = @RecordStatus, ModifiedOn = GETDATE(), ModifiedBy = @ModifiedBy, ModifiedName = @ModifiedName
                    WHERE AnswerKey = @AnswerKey AND RecordStatus != 99";

                string connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
                using (var conn = new SqlConnection(connectionString))
                {
                    try
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@AnswerKey", Guid.Parse(_AnswerKey));
                            cmd.Parameters.AddWithValue("@AnswerText", (object)_AnswerText ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@AnswerCorrect", _AnswerCorrect);
                            cmd.Parameters.AddWithValue("@Ranking", _Ranking);
                            cmd.Parameters.AddWithValue("@Category", string.IsNullOrEmpty(_Category) ? DBNull.Value : Guid.Parse(_Category));
                            cmd.Parameters.AddWithValue("@GrammarTopic", string.IsNullOrEmpty(_GrammarTopic) ? DBNull.Value : Guid.Parse(_GrammarTopic));
                            cmd.Parameters.AddWithValue("@ErrorType", string.IsNullOrEmpty(_ErrorType) ? DBNull.Value : Guid.Parse(_ErrorType));
                            cmd.Parameters.AddWithValue("@RecordStatus", _RecordStatus);
                            cmd.Parameters.AddWithValue("@ModifiedBy", string.IsNullOrEmpty(_ModifiedBy) ? DBNull.Value : Guid.Parse(_ModifiedBy));
                            cmd.Parameters.AddWithValue("@ModifiedName", (object)_ModifiedName ?? DBNull.Value);

                            int rowsAffected = cmd.ExecuteNonQuery();
                            if (rowsAffected > 0)
                            {
                                _Status = "OK";
                                _Message = "Answer updated successfully";
                            }
                            else
                            {
                                _Status = "ERROR";
                                _Message = "No record found to update";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _Status = "ERROR";
                        _Message = $"Error updating answer: {ex.Message}";
                    }
                }
            }

            public void Delete()
            {
                if (string.IsNullOrEmpty(_AnswerKey) || _AnswerKey.Length != 36)
                {
                    _Status = "ERROR";
                    _Message = "Invalid AnswerKey";
                    return;
                }

                string sql = "UPDATE [dbo].[TEC_Part2_Answer] SET RecordStatus = 99 WHERE AnswerKey = @AnswerKey AND RecordStatus != 99";
                string connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
                using (var conn = new SqlConnection(connectionString))
                {
                    try
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@AnswerKey", Guid.Parse(_AnswerKey));
                            int rowsAffected = cmd.ExecuteNonQuery();
                            if (rowsAffected > 0)
                            {
                                _Status = "OK";
                                _Message = "Answer deleted successfully";
                            }
                            else
                            {
                                _Status = "ERROR";
                                _Message = "No record found to delete";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _Status = "ERROR";
                        _Message = $"Error deleting answer: {ex.Message}";
                    }
                }
            }
            #endregion
        }
    }
}
