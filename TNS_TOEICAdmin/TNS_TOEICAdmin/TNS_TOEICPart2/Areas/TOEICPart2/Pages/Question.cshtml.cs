using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.IO;
using System;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using TNS_TOEICPart2.Areas.TOEICPart2.Models;

namespace TNS_TOEICPart2.Areas.TOEICPart2.Pages
{
    [IgnoreAntiforgeryToken]
    public class QuestionModel : PageModel
    {
        #region [ Security ]
        public TNS_Auth.UserLogin_Info UserLogin;
        public string QuestionKey;
        public bool IsFullAdmin { get; private set; }
        private void CheckAuth()
        {
            UserLogin = new TNS_Auth.UserLogin_Info(User);

            // Kiểm tra quyền Full trước
            var fullRole = new TNS_Auth.Role_Info(UserLogin.UserKey, "Full");
            if (fullRole.GetCode() == "200") // Có quyền Full trong DB
            {
                IsFullAdmin = true;
                UserLogin.GetRole("Questions"); // Vẫn lấy nhưng không ảnh hưởng
            }
            else
            {
                IsFullAdmin = false;
                UserLogin.GetRole("Questions"); // Lấy quyền Questions
            }

            // Đảm bảo Role được khởi tạo
            if (UserLogin.Role == null)
            {
                UserLogin.GetRole("Questions");
            }
        }
        #endregion
        private readonly IWebHostEnvironment _env;
        public QuestionModel(IWebHostEnvironment env)
        {
            _env = env;
        }

        public IActionResult OnGet(string key = null)
        {
            CheckAuth();
            if (UserLogin.Role.IsRead || IsFullAdmin)
            {
                QuestionKey = key;
                return Page();
            }
            else
            {
                TempData["Error"] = "ACCESS DENIED!!!";
                return Page();
            }
        }

        #region [ Record CRUD]
        public IActionResult OnPostRecordRead([FromBody] ItemRequest request)
        {
            CheckAuth();
            JsonResult zResult = new JsonResult("");
            if (UserLogin.Role.IsRead || IsFullAdmin)
            {
                QuestionAccessData.Part2_Question_Info zRecord;
                if (string.IsNullOrEmpty(request.QuestionKey) || request.QuestionKey.Length != 36)
                    zRecord = new QuestionAccessData.Part2_Question_Info();
                else
                    zRecord = new QuestionAccessData.Part2_Question_Info(request.QuestionKey);
                zResult = new JsonResult(zRecord);
            }
            else
            {
                zResult = new JsonResult(new { Status = "ERROR", Result = "ACCESS DENIED" });
            }
            return zResult;
        }

        public IActionResult OnPostGetInfo([FromBody] ItemRequest request)
        {
            CheckAuth();
            if (!UserLogin.Role.IsRead || !IsFullAdmin)
                return new JsonResult(new { status = "ERROR", message = "ACCESS DENIED" });

            var record = new QuestionAccessData.Part2_Question_Info(request.QuestionKey);
            return new JsonResult(new
            {
                CreatedOn = record.CreatedOn?.ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedBy = record.CreatedName,
                ModifiedOn = record.ModifiedOn?.ToString("yyyy-MM-dd HH:mm:ss"),
                ModifiedBy = record.ModifiedName
            });
        }
        public IActionResult OnPostLoadDropdowns()
        {
            CheckAuth();
            if (!UserLogin.Role.IsRead || !IsFullAdmin)
                return new JsonResult(new { status = "ERROR", message = "ACCESS DENIED" });

            string connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var categories = new List<object>();
                var grammarTopics = new List<object>();
                var vocabularyTopics = new List<object>();
                var errorTypes = new List<object>();

                // Load Categories
                //using (SqlCommand cmd = new SqlCommand("SELECT CategoryKey, CategoryName FROM [dbo].[TEC_Category] WHERE Part = 1", conn))
                using (SqlCommand cmd = new SqlCommand("SELECT CategoryKey, CategoryName FROM [dbo].[TEC_Category]", conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            categories.Add(new { CategoryKey = reader["CategoryKey"].ToString(), CategoryName = reader["CategoryName"].ToString() });
                        }
                    }
                }

                // Load Grammar Topics
                using (SqlCommand cmd = new SqlCommand("SELECT GrammarTopicID, TopicName FROM [dbo].[GrammarTopics]", conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            grammarTopics.Add(new { GrammarTopicID = reader["GrammarTopicID"].ToString(), TopicName = reader["TopicName"].ToString() });
                        }
                    }
                }

                // Load Vocabulary Topics
                using (SqlCommand cmd = new SqlCommand("SELECT VocabularyTopicID, TopicName FROM [dbo].[VocabularyTopics]", conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            vocabularyTopics.Add(new { VocabularyTopicID = reader["VocabularyTopicID"].ToString(), TopicName = reader["TopicName"].ToString() });
                        }
                    }
                }

                // Load Error Types
                using (SqlCommand cmd = new SqlCommand("SELECT ErrorTypeID, ErrorDescription FROM [dbo].[ErrorTypes]", conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            errorTypes.Add(new { ErrorTypeID = reader["ErrorTypeID"].ToString(), ErrorDescription = reader["ErrorDescription"].ToString() });
                        }
                    }
                }

                return new JsonResult(new
                {
                    categories,
                    grammarTopics,
                    vocabularyTopics,
                    errorTypes
                });
            }
        }

        public IActionResult OnPostRecordCreate()
        {
            CheckAuth();
            if (!UserLogin.Role.IsCreate || !IsFullAdmin)
                return new JsonResult(new { status = "ERROR", message = "ACCESS DENIED" });

            string recordJson = HttpContext.Request.Form["record"];
            var zRecord = JsonSerializer.Deserialize<QuestionAccessData.Part2_Question_Info>(recordJson);
            zRecord.CreatedBy = UserLogin.Employee.Key;
            zRecord.CreatedName = UserLogin.Employee.Name;

            string wwwPath = _env.WebRootPath;
            string zQuestionKey = zRecord.QuestionKey ?? Guid.NewGuid().ToString();
            //string imgPath = Path.Combine(wwwPath, $"upload/question/{zQuestionKey}/img");
            string audioPath = Path.Combine(wwwPath, $"upload/question/{zQuestionKey}/audio");

            try
            {
                //Directory.CreateDirectory(imgPath);
                Directory.CreateDirectory(audioPath);

                //var imageFile = HttpContext.Request.Form.Files["image"];
                //if (imageFile != null && imageFile.Length > 0)
                //{
                //    string filePath = Path.Combine(imgPath, imageFile.FileName);
                //    using (var stream = new FileStream(filePath, FileMode.Create))
                //    {
                //        imageFile.CopyTo(stream);
                //    }
                //    zRecord.QuestionImage = $"/upload/question/{zQuestionKey}/img/{imageFile.FileName}";
                //}

                var voiceFile = HttpContext.Request.Form.Files["voice"];
                if (voiceFile != null && voiceFile.Length > 0)
                {
                    string filePath = Path.Combine(audioPath, voiceFile.FileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        voiceFile.CopyTo(stream);
                    }
                    zRecord.QuestionVoice = $"/upload/question/{zQuestionKey}/audio/{voiceFile.FileName}";
                }

                zRecord.Create();
                return new JsonResult(new { status = zRecord.Status, message = zRecord.Message, record = zRecord });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { status = "ERROR", message = $"Failed to create record: {ex.Message}" });
            }
        }

        public IActionResult OnPostRecordUpdate()
        {
            CheckAuth();
            if (!UserLogin.Role.IsUpdate || !IsFullAdmin)
                return new JsonResult(new { status = "ERROR", message = "ACCESS DENIED" });

            string recordJson = HttpContext.Request.Form["record"];
            var zRecord = JsonSerializer.Deserialize<QuestionAccessData.Part2_Question_Info>(recordJson);
            zRecord.ModifiedBy = UserLogin.Employee.Key;
            zRecord.ModifiedName = UserLogin.Employee.Name;

            string wwwPath = _env.WebRootPath;
            string zQuestionKey = zRecord.QuestionKey;
            //string imgPath = Path.Combine(wwwPath, $"upload/question/{zQuestionKey}/img");
            string audioPath = Path.Combine(wwwPath, $"upload/question/{zQuestionKey}/audio");

            try
            {
                //Directory.CreateDirectory(imgPath);
                Directory.CreateDirectory(audioPath);

                //var imageFile = HttpContext.Request.Form.Files["image"];
                //if (imageFile != null && imageFile.Length > 0)
                //{
                //    if (!string.IsNullOrEmpty(zRecord.QuestionImage) && System.IO.File.Exists(Path.Combine(wwwPath, zRecord.QuestionImage.TrimStart('/'))))
                //    {
                //        System.IO.File.Delete(Path.Combine(wwwPath, zRecord.QuestionImage.TrimStart('/')));
                //    }
                //    string filePath = Path.Combine(imgPath, imageFile.FileName);
                //    using (var stream = new FileStream(filePath, FileMode.Create))
                //    {
                //        imageFile.CopyTo(stream);
                //    }
                //    zRecord.QuestionImage = $"/upload/question/{zQuestionKey}/img/{imageFile.FileName}";
                //}

                var voiceFile = HttpContext.Request.Form.Files["voice"];
                if (voiceFile != null && voiceFile.Length > 0)
                {
                    if (!string.IsNullOrEmpty(zRecord.QuestionVoice) && System.IO.File.Exists(Path.Combine(wwwPath, zRecord.QuestionVoice.TrimStart('/'))))
                    {
                        System.IO.File.Delete(Path.Combine(wwwPath, zRecord.QuestionVoice.TrimStart('/')));
                    }
                    string filePath = Path.Combine(audioPath, voiceFile.FileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        voiceFile.CopyTo(stream);
                    }
                    zRecord.QuestionVoice = $"/upload/question/{zQuestionKey}/audio/{voiceFile.FileName}";
                }

                zRecord.Update();
                return new JsonResult(new { status = zRecord.Status, message = zRecord.Message, record = zRecord });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { status = "ERROR", message = $"Failed to update record: {ex.Message}" });
            }
        }

        public IActionResult OnPostRecordDel([FromBody] ItemRequest request)
        {
            CheckAuth();
            JsonResult zResult = new JsonResult("");
            if (UserLogin.Role.IsDelete || IsFullAdmin)
            {
                if (request == null || string.IsNullOrEmpty(request.QuestionKey))
                {
                    zResult = new JsonResult(new { Status = "ERROR", Message = "QuestionKey is missing" });
                }
                else
                {
                    QuestionAccessData.Part2_Question_Info zRecord = new QuestionAccessData.Part2_Question_Info();
                    zRecord.QuestionKey = request.QuestionKey;
                    zRecord.Delete();
                    if (zRecord.Status == "OK")
                        zResult = new JsonResult(new { Status = "OK", Message = "" });
                    else
                        zResult = new JsonResult(new { Status = "ERROR", Message = zRecord.Message });
                }
            }
            else
            {
                zResult = new JsonResult(new { Status = "ERROR", Message = "ACCESS DENIED" });
            }
            return zResult;
        }

        public IActionResult OnPostRecordDell([FromBody] ItemRequest request)
        {
            CheckAuth();
            JsonResult zResult = new JsonResult("");
            if (UserLogin.Role.IsDelete || IsFullAdmin)
            {
                if (request == null || string.IsNullOrEmpty(request.QuestionKey))
                {
                    zResult = new JsonResult(new { Status = "ERROR", Message = "QuestionKey is missing" });
                }
                else
                {
                    QuestionAccessData.Part2_Question_Info zRecord = new QuestionAccessData.Part2_Question_Info(request.QuestionKey);
                    if (zRecord.Status == "OK")
                    {
                        string wwwPath = _env.WebRootPath;
                        //if (!string.IsNullOrEmpty(zRecord.QuestionImage))
                        //{
                        //    string imagePath = Path.Combine(wwwPath, zRecord.QuestionImage.TrimStart('/'));
                        //    if (System.IO.File.Exists(imagePath))
                        //        System.IO.File.Delete(imagePath);
                        //}
                        if (!string.IsNullOrEmpty(zRecord.QuestionVoice))
                        {
                            string voicePath = Path.Combine(wwwPath, zRecord.QuestionVoice.TrimStart('/'));
                            if (System.IO.File.Exists(voicePath))
                                System.IO.File.Delete(voicePath);
                        }

                        string questionDir = Path.Combine(wwwPath, $"upload/question/{request.QuestionKey}");
                        if (Directory.Exists(questionDir) && Directory.GetFiles(questionDir, "*", SearchOption.AllDirectories).Length == 0)
                            Directory.Delete(questionDir, true);

                        zRecord.Empty();
                        if (zRecord.Status == "OK")
                            zResult = new JsonResult(new { Status = "OK", Message = "Question deleted permanently" });
                        else
                            zResult = new JsonResult(new { Status = "ERROR", Message = zRecord.Message });
                    }
                    else
                    {
                        zResult = new JsonResult(new { Status = "ERROR", Message = "Question not found" });
                    }
                }
            }
            else
            {
                zResult = new JsonResult(new { Status = "ERROR", Message = "ACCESS DENIED" });
            }
            return zResult;
        }
        #endregion

        public class ItemRequest
        {
            public string QuestionKey { get; set; }
        }
    }
}
