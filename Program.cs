using IMS;
using IMS.Data;
using Microsoft.EntityFrameworkCore;
using Syncfusion.Licensing; // Add this at the top

SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JFaF5cXGRCf1NpRnxbf1x1ZFNMYlVbQHRPMyBoS35Rc0VqW3lednZcQmJeWEF1VEFc");

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllersWithViews();
builder.Services.AddSession();
builder.Services.AddControllersWithViews().AddSessionStateTempDataProvider();

builder.Services.AddDbContext<MyAppContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnectionString")));

var app = builder.Build();

// Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();
app.UseRouting();
app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
