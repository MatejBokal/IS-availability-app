using AvailabilityCollector.Data;
using AvailabilityCollector.Models;
using AvailabilityCollector.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Identity
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Controllers and Views
builder.Services.AddControllers();
builder.Services.AddControllersWithViews();

// Configure route options for lowercase URLs
builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true;
});

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
builder.Services.AddRazorPages();

// JWT configuration
var jwtSection = builder.Configuration.GetSection("Jwt");
var issuer = jwtSection["Issuer"];
var audience = jwtSection["Audience"];
var key = jwtSection["Key"] ?? throw new Exception("Jwt:Key missing");

// Authentication: Cookies (default for web UI) + JWT (for API)
builder.Services
    .AddAuthentication(options =>
    {
        // Default scheme is cookies (Identity) for web UI
        // JWT Bearer is added as an additional scheme for API
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddAuthorization();

// Background service for auto-locking months
builder.Services.AddHostedService<AutoLockService>();

// Background service for notification reminders
builder.Services.AddHostedService<NotificationReminderService>();

// Notification service
builder.Services.AddScoped<NotificationService>();

// Swagger + JWT support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AvailabilityCollector API", Version = "v1" });

    // Only include API routes (routes starting with /api)
    c.DocInclusionPredicate((docName, apiDesc) =>
    {
        return apiDesc.RelativePath?.StartsWith("api/", StringComparison.OrdinalIgnoreCase) == true;
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Enable Swagger UI in all environments (required for assignment documentation)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AvailabilityCollector API v1");
    c.RoutePrefix = "swagger"; // Swagger UI will be available at /swagger
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Enable CORS for Android app
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute(
    name: "dashboard",
    pattern: "dashboard",
    defaults: new { controller = "Dashboard", action = "Index" });
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=home}/{action=index}/{id?}")
    .WithStaticAssets();
app.MapRazorPages();

// Seed roles and admin user
await SeedData.EnsureSeededAsync(app.Services);

app.Run();
