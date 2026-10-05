using gamabelmvc.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.WebUtilities;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddFastReport();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(15);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".Gamabel.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
        options.SlidingExpiration = true;
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Events.OnValidatePrincipal = context =>
        {
            var issuedAt = context.Principal?.FindFirst("auth_time")?.Value;
            if (!long.TryParse(issuedAt, out var issuedAtUnix)
                || DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(issuedAtUnix) > TimeSpan.FromHours(8))
            {
                context.RejectPrincipal();
                return context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }

            return Task.CompletedTask;
        };
        options.Events.OnRedirectToLogin = context =>
        {
            var isStsRequest = context.HttpContext.Session.GetString("ActiveModule") == "STS"
                || new[] { "Eksik", "Sevkiyat", "Siparis", "RaporSts", "Admin" }
                    .Contains(context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), StringComparer.OrdinalIgnoreCase);

            if (context.Request.Headers.XRequestedWith == "XMLHttpRequest")
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            var loginPath = isStsRequest ? "/Account/StsLogin" : options.LoginPath.Value ?? "/Account/Login";
            context.Response.Redirect(QueryHelpers.AddQueryString(loginPath, "ReturnUrl", context.Request.Path + context.Request.QueryString));
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            var request = context.Request;
            var isPageNavigation =
                (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)) &&
                request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase) &&
                request.Headers.XRequestedWith != "XMLHttpRequest";

            if (isPageNavigation)
                context.Response.Redirect("/Home/Index");
            else
                context.Response.StatusCode = StatusCodes.Status403Forbidden;

            return Task.CompletedTask;
        };
    });

builder.Services.AddSingleton<IAuthorizationHandler, AppAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("PrsModule", policy => policy.AddRequirements(new ModuleRequirement("Personel")));
    options.AddPolicy("StsModule", policy => policy.AddRequirements(new ModuleRequirement("STS")));
    options.AddPolicy("StsWarehouse", policy =>
    {
        policy.AddRequirements(new ModuleRequirement("STS"));
        policy.AddRequirements(new RoleRequirement("DepoSorumlusu", "Admin"));
    });
    options.AddPolicy("StsAdmin", policy =>
    {
        policy.AddRequirements(new ModuleRequirement("STS"));
        policy.AddRequirements(new RoleRequirement("Admin"));
    });
    options.AddPolicy("PrsAdmin", policy =>
    {
        policy.AddRequirements(new ModuleRequirement("Personel"));
        policy.AddRequirements(new RoleRequirement("admin"));
    });
    options.AddPolicy("PrsMailAdmin", policy =>
    {
        policy.AddRequirements(new ModuleRequirement("Personel"));
        policy.AddRequirements(new RoleRequirement("admin"));
    });
    options.AddPolicy("ComplaintAdmin", policy =>
    {
        policy.AddRequirements(new ModuleRequirement("Personel"));
        policy.AddRequirements(new RoleRequirement("admin"));
    });
    options.AddPolicy("PrsMenuPersonel", policy => PrsMenuPolicy(policy, "menu_personel"));
    options.AddPolicy("PrsMenuPuantaj", policy => PrsMenuPolicy(policy, "menu_puantaj"));
    options.AddPolicy("PrsMenuRapor", policy => PrsMenuPolicy(policy, "menu_rapor"));
    options.AddPolicy("PrsMenuMesai", policy =>
    {
        policy.AddRequirements(new ModuleRequirement("Personel"));
        policy.AddRequirements(new MenuRequirement("menu_mesai", "birim_amiri", "birim amiri"));
    });
    options.AddPolicy("PrsMenuTatil", policy => PrsMenuPolicy(policy, "menu_tatiller"));
    options.AddPolicy("PrsMenuOdeme", policy => PrsMenuPolicy(policy, "menu_odeme_talimat"));
    options.AddPolicy("PrsMenuYetkilendirme", policy => PrsMenuPolicy(policy, "menu_yetkilendirme"));
    options.AddPolicy("PrsMenuDokumantasyon", policy => PrsMenuPolicy(policy, "menu_dokumantasyon"));
});

// View locations - STS ve PRS modüllerini destekle
builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    options.ViewLocationFormats.Clear();
    options.ViewLocationFormats.Add("/Views/{1}/{0}.cshtml");
    options.ViewLocationFormats.Add("/Views/{0}.cshtml");
    options.ViewLocationFormats.Add("/Views/STS/{1}/{0}.cshtml");
    options.ViewLocationFormats.Add("/Views/PRS/{1}/{0}.cshtml");
    options.ViewLocationFormats.Add("/Views/Shared/{0}.cshtml");
    options.ViewLocationFormats.Add("/Views/Account/{0}.cshtml");
    
    options.PageViewLocationFormats.Clear();
    options.PageViewLocationFormats.Add("/Views/{0}.cshtml");
    options.PageViewLocationFormats.Add("/Views/Shared/{0}.cshtml");
});

// STS DbConnectionFactory servisi - IWebHostEnvironment ile injectionmu
builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddSingleton<IWebHostEnvironment>(builder.Environment);

// PRS hizmetleri
builder.Services.AddSingleton<OdemeFaturaImportService>();
builder.Services.AddSingleton<OdemeTalimatService>();

// Sistem bilgileri servisi (IP, MAC, Bilgisayar Adı vb.)
builder.Services.AddSingleton<ISystemInfoService, SystemInfoService>();

// Mail gönderim servisleri
builder.Services.AddScoped<IMailService, MailService>();
// MailQueueService'i hem singleton olarak erişilebilir kıl ve HostedService olarak çalıştır
builder.Services.AddSingleton<MailQueueService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<MailQueueService>());

// Infobip WhatsApp mesaj gönderimi
builder.Services.AddHttpClient<IWhatsAppMessageSender, InfobipWhatsAppMessageSender>();



var app = builder.Build();

// Seed test user in Development mode
if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var cs = config.GetConnectionString("MyConnection");
        
        await using var conn = new MySqlConnection(cs);
        await conn.OpenAsync();
        
        // Delete existing test user if present
        var deleteCmd = new MySqlCommand("DELETE FROM admin_kullanicilar WHERE kullanici_adi = 'test'", conn);
        await deleteCmd.ExecuteNonQueryAsync();
        
        // Add aktif_mi column if it doesn't exist
        try {
            var alterCmd = new MySqlCommand("ALTER TABLE admin_kullanicilar ADD COLUMN aktif_mi BOOLEAN DEFAULT TRUE", conn);
            await alterCmd.ExecuteNonQueryAsync();
        } catch { /* Column might already exist */ }
        
        // Insert test user as admin with aktif_mi=true
        var cmd = new MySqlCommand(
            "INSERT INTO admin_kullanicilar (kullanici_adi, sifre, birim, rol, aktif_mi) VALUES ('test', 'test', 'AÇIK PAZAR', 'admin', TRUE)",
            conn);
        await cmd.ExecuteNonQueryAsync();
        
        await conn.CloseAsync();
    }
    catch { /* Silently skip if error */ }
}

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

app.UseSession();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
            return Task.CompletedTask;
        });
    }

    await next();
});
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.UseFastReport();

app.Run();

static void PrsMenuPolicy(AuthorizationPolicyBuilder policy, string menuClaim)
{
    policy.AddRequirements(new ModuleRequirement("Personel"));
    policy.AddRequirements(new MenuRequirement(menuClaim));
}
