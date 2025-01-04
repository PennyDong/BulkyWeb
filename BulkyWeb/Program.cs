using Bulky.DataAccess.Data;
using Bulky.DataAccess.Repository;
using Bulky.DataAccess.Repository.IRepository;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Bulky.Utility;
using Microsoft.AspNetCore.Identity.UI.Services;
using Bulky.DataAccess.DbInitializer;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.OAuth;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// 註冊Session服務
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.IdleTimeout = TimeSpan.FromMinutes(300);  // 設定Session過期時間
    options.Cookie.IsEssential = true;  // 確保此Cookie是必要的
});


builder.Services.AddDbContext<ApplicationDbContext>(options => 
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

//builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));
builder.Services.AddTransient<ECPayPaymentClient>();
builder.Services.Configure<ECPayPaymentClient>(builder.Configuration.GetSection("ECPay"));
builder.Services.Configure<FacebookSettings>(builder.Configuration.GetSection("Facebook"));
//添加人員與角色
builder.Services.AddIdentity<IdentityUser,IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();

//必須在人員與角色權限後面添加
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = $"/Identity/Account/Login";
    options.LogoutPath = $"/Identity/Account/Logout";
    options.AccessDeniedPath = $"/Identity/Account/AccessDenied";
});

//facebook
builder.Services.AddAuthentication().AddFacebook(option =>
{
    var facebookSettings = builder.Services.BuildServiceProvider().GetRequiredService<IOptions<FacebookSettings>>().Value;
    option.AppId = facebookSettings.AppId;
    option.AppSecret = facebookSettings.AppSecret;

    option.Events = new OAuthEvents
    {
        //當詢問授權時，如果按下取消返回首頁。
        OnRemoteFailure = context =>
        {
            //Console.WriteLine($"Remote failure: {context.Failure?.Message}");
            context.Response.Redirect($"/");
            context.HandleResponse();
            return Task.CompletedTask;
        }
    };
});


//session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(1800);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});



builder.Services.AddScoped<IDbInitializer, DbInitializer>();
builder.Services.AddRazorPages();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IEmailSender, EmailSender>();
var app = builder.Build();
/*
 每次執行都會啟動，檢查是否有管理者帳號，如果沒有則自動創建。
 */
SeedDatabase();
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication(); //身分驗證
app.UseAuthorization(); //授權
app.UseSession();



app.MapRazorPages(); //Razor頁面
app.MapControllerRoute(
    name: "default",
    pattern: "{area=Customer}/{controller=Home}/{action=Index}/{id?}");

app.Run();

void SeedDatabase()
{
    using(var scope = app.Services.CreateScope())
    {
        var dbInitializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
        dbInitializer.Initialize();
    }
}
