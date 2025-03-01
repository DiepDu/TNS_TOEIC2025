using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// 🔹 Thêm dịch vụ vào container
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Tasks"); // Yêu cầu Authentication cho thư mục /Tasks
    options.Conventions.AuthorizePage("/Index");   // Yêu cầu Authentication cho trang /Index
});

// Thêm Controllers nếu cần
builder.Services.AddControllers();

// 🔹 Cấu hình Authentication bằng Cookie
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();

builder.Services.AddHttpContextAccessor(); // Đăng ký IHttpContextAccessor

var app = builder.Build();

// 🔹 Cấu hình Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication(); // Thêm Middleware Authentication
app.UseAuthorization();

app.MapControllers();  // Nếu có API Controller
app.MapRazorPages();   // Map Razor Pages

app.Run();
