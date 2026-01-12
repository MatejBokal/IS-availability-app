using AvailabilityCollector.Data;
using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.Identity;
using AvailabilityCollector.Models;
using AvailabilityCollector.Data;



var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppContextDb>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AppContext")));

var connectionString = builder.Configuration.GetConnectionString("AppContext");

builder.Services.AddDbContext<AppIdentityContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDbContext<AppContextDb>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<AppIdentityContext>();


// Add services to the container.
builder.Services.AddControllersWithViews();

// Add CORS for Android app
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// Enable CORS for Android app
app.UseCors("AllowAll");

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages();

app.Run();
