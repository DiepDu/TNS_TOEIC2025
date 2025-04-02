using Microsoft.AspNetCore.Authentication.Cookies;
using TNS_TOEICTest.DataAccess;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers(); // Đăng ký dịch vụ cho API Controller
builder.Services.AddRazorPages();  // Đăng ký dịch vụ cho Razor Pages

builder.Services.AddRazorPages(options =>
{
    // Yêu cầu đăng nhập cho trang Test
    options.Conventions.AuthorizeAreaPage("Test", "/Test");
    // Route cho trang Test
    options.Conventions.AddAreaPageRoute("Test", "/Test", "Test/Test");
    // Route cho trang ResultTest
    options.Conventions.AddAreaPageRoute("Test", "/ResultTest", "Test/ResultTest");
});
builder.Services.AddSingleton<CreateAccountAccessData>();
builder.Services.AddSingleton<CreateAccountAccessData>();
builder.Services.AddSingleton<ProfileAccessData>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // Giữ nguyên tên thuộc tính JSON
    });

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

app.Run();