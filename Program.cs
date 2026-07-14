using gamabelmvc.Services;
using Microsoft.AspNetCore.Mvc.Razor;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSession();

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

// Sistem bilgileri servisi (IP, MAC, Bilgisayar Adı vb.)
builder.Services.AddSingleton<ISystemInfoService, SystemInfoService>();

// Ödeme Talimat Sistemi servisleri
builder.Services.AddScoped<gamabelmvc.Services.OdemeFaturaImportService>();
builder.Services.AddScoped<gamabelmvc.Services.OdemeTalimatService>();

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
app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
