using LDHub.Data;
using LDHub.Repositories;
using LDHub.Repositories.Interfaces;
using LDHub.Services;
using LDHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── 1. DATABASE CONNECTION ────────────────────────────────────────
// Connects to MySQL using the connection string from appsettings.json
// Equivalent to include config.php in PHP
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(
            builder.Configuration.GetConnectionString("DefaultConnection")
        )
    )
);

// ── 2. REGISTER SERVICES (Dependency Injection) ───────────────────
// This is how .NET wires everything together
// When a controller asks for ILocationService, .NET gives it LocationService
// AddScoped = one instance per HTTP request (most common for web apps)

// Location (Month 1 — practice CRUD)
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
builder.Services.AddScoped<ILocationService, LocationService>();

// More services will be added here in Month 2, 3, 4, 5...
// builder.Services.AddScoped<IAuthService, AuthService>();
// builder.Services.AddScoped<IRegistrationService, RegistrationService>();
// builder.Services.AddScoped<IApprovalService, ApprovalService>();
// builder.Services.AddScoped<IEmailService, EmailService>();
// builder.Services.AddScoped<ITrainingService, TrainingService>();
// builder.Services.AddScoped<IModuleService, ModuleService>();
// builder.Services.AddScoped<IQuizService, QuizService>();
// builder.Services.AddScoped<IAssessmentService, AssessmentService>();
// builder.Services.AddScoped<ICertificateService, CertificateService>();

// ── 3. SESSION SETUP ──────────────────────────────────────────────
// Replaces PHP's $_SESSION
// Stores: userId, userName, userPhoto, activeRole, userRoles
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);  // Session expires after 60 mins idle
    options.Cookie.HttpOnly = true;     // Prevents JS from accessing session cookie
    options.Cookie.IsEssential = true;  // Required even when user declines cookies
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;  // HTTPS only
});

// ── 4. MVC ────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

var app = builder.Build();

// ── 5. MIDDLEWARE PIPELINE ────────────────────────────────────────
// Order matters! Each request passes through these in sequence

if (!app.Environment.IsDevelopment())
{
    // Show friendly error page in production
    app.UseExceptionHandler("/Home/Error");
    // HSTS — tells browsers to always use HTTPS (IIS requirement)
    app.UseHsts();
}

app.UseHttpsRedirection();  // Redirect HTTP → HTTPS automatically
app.UseStaticFiles();        // Serve files from wwwroot/ (CSS, JS, images)
app.UseRouting();            // Match incoming URLs to controllers
app.UseSession();            // Enable session BEFORE authorization
app.UseAuthorization();      // Check role permissions

// ── 6. DEFAULT ROUTE ──────────────────────────────────────────────
// When someone visits the site, send them to Registration/Register
// This will change to Auth/Login in Month 2
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();