using TNS_TOEICTest.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using TNS_TOEICTest.DataAccess;
using TNS_TOEICTest.Hubs;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddRazorPages();

builder.Services.AddSingleton<IUserConnectionManager, UserConnectionManager>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowChat", builder =>
    {
        builder.WithOrigins("https://localhost:7078", "https://localhost:7003", "http://localhost:3000")
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeAreaPage("Test", "/Test");
    options.Conventions.AddAreaPageRoute("Test", "/Test", "Test/Test");
    options.Conventions.AddAreaPageRoute("Test", "/ResultTest", "Test/ResultTest");
});
builder.Services.AddSingleton<CreateAccountAccessData>();
builder.Services.AddSingleton<ProfileAccessData>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(60);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCors("AllowChat");

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();
app.MapHub<ChatHub>("/chatHub");

app.Run();