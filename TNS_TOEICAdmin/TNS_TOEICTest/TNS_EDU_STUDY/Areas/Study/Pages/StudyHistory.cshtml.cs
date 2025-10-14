using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using TNS_EDU_TEST.Areas.Test.Models; // Đảm bảo namespace này đúng

namespace TNS_EDU_STUDY.Areas.Study.Pages // Bạn có thể đổi namespace này cho phù hợp
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class StudyHistoryModel : PageModel
    {
        // Thuộc tính để nhận giá trị Part được chọn từ URL, mặc định là 1
        [BindProperty(SupportsGet = true)]
        public int PartSelect { get; set; } = 1;

        // Danh sách để lưu trữ lịch sử các bài làm
        public List<StudyHistoryItem> StudyHistoryItems { get; set; }

        // Chuỗi JSON để truyền dữ liệu sang cho JavaScript vẽ biểu đồ
        public string StudyHistoryJson { get; set; }

        // Xử lý khi trang được tải lần đầu (GET request)
        public void OnGet()
        {
            // Lấy MemberKey của người dùng đang đăng nhập
            string memberKey = User.Claims.FirstOrDefault(c => c.Type == "MemberKey" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(memberKey))
            {
                // Nếu không tìm thấy user, khởi tạo danh sách rỗng và thoát
                StudyHistoryItems = new List<StudyHistoryItem>();
                StudyHistoryJson = "[]";
                return;
            }

            // Tải lịch sử làm bài cho Part mặc định (Part 1)
            StudyHistoryItems = StudyHistoryAccessData.LoadStudyHistory(memberKey, this.PartSelect);

            // Chuyển đổi danh sách lịch sử thành chuỗi JSON để JavaScript sử dụng
            // Sắp xếp theo ngày tạo để biểu đồ hiển thị đúng thứ tự thời gian
            var sortedHistory = StudyHistoryItems.OrderBy(item => item.CreatedOn).ToList();
            StudyHistoryJson = JsonSerializer.Serialize(sortedHistory, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase // Chuyển tên thuộc tính thành camelCase (vd: PracticeScore -> practiceScore)
            });
        }

        // API Handler để xử lý yêu cầu AJAX khi người dùng đổi Part trên combobox
        // API Handler để xử lý yêu cầu AJAX khi người dùng đổi Part trên combobox
        public IActionResult OnGetHistoryByPart(int part)
        {
            // Lấy MemberKey của người dùng
            string memberKey = User.Claims.FirstOrDefault(c => c.Type == "MemberKey" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(memberKey))
            {
                // Trả về lỗi nếu không có thông tin người dùng
                return Unauthorized();
            }

            // Tải lịch sử cho Part được yêu cầu
            var historyItems = StudyHistoryAccessData.LoadStudyHistory(memberKey, part);

            // Sắp xếp lại để biểu đồ hiển thị đúng
            var sortedHistory = historyItems.OrderBy(item => item.CreatedOn).ToList();

            // THAY ĐỔI Ở ĐÂY: Thêm JsonSerializerOptions để đảm bảo camelCase
            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // Trả về dữ liệu dưới dạng JSON với tùy chọn đã chỉ định
            return new JsonResult(sortedHistory, serializerOptions);
        }
    }
}
