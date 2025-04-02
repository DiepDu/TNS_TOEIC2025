using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TNS_TOEICTest.DataAccess;

namespace TNS_TOEICTest.Pages.Account
{
    [IgnoreAntiforgeryToken]
    public class CreateAccountModel : PageModel
    {
        [BindProperty]
        public string FullName { get; set; }

        [BindProperty]
        public string Gender { get; set; }

        [BindProperty]
        public int BirthYear { get; set; }

        [BindProperty]
        public string Email { get; set; }

        [BindProperty]
        public string Password { get; set; }

        [BindProperty]
        public string ConfirmPassword { get; set; }

        public string ErrorMessage { get; set; }

        private readonly CreateAccountAccessData _dataAccess;

        // Constructor để inject CreateAccountAccessData qua DI
        public CreateAccountModel(CreateAccountAccessData dataAccess)
        {
            _dataAccess = dataAccess;
        }

        public void OnGet()
        {
            // Hiển thị trang tạo tài khoản
        }

        public IActionResult OnPostCreateAccount()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (Password != ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
                return Page();
            }

            // Kiểm tra Email trùng lặp
            if (_dataAccess.CheckEmailExists(Email))
            {
                ErrorMessage = "Email đã tồn tại. Vui lòng sử dụng Email khác.";
                return Page();
            }

            // Tạo dữ liệu để lưu vào database
            var memberKey = Guid.NewGuid();
            var genderValue = Gender == "Male" ? 1 : 0; // Nam = 1, Nữ = 0
            var createOn = DateTime.Now;
            var createBy = memberKey;
            var createName = FullName;
            var active = 1;

            // Mã hóa mật khẩu bằng HashPass
            var hashedPassword = TNS.Member.MyCryptography.HashPass(Password);

            // Gọi lớp DataAccess để lưu vào database
            bool isSuccess = _dataAccess.CreateMember(
                memberKey,
                Email, // MemberID
                FullName, // MemberName
                genderValue,
                BirthYear,
                createOn,
                createBy,
                createName,
                active,
                hashedPassword
            );

            if (isSuccess)
            {
                return RedirectToPage("/Login"); // Chuyển hướng sau khi tạo tài khoản thành công
            }
            else
            {
                ErrorMessage = "Failed to create account. Please try again.";
                return Page();
            }
        }
    }
}