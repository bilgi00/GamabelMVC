# 🚀 GAMABEL MVC - DEPLOYMENT REHBERI

**Tarih**: 10 Haziran 2026  
**Versiyon**: 3.2  
**Durum**: ✅ Üretim'e Hazır

---

## 📋 İçindekiler

1. [Ön Koşullar](#ön-koşullar)
2. [Yerel Geliştirme](#yerel-geliştirme)
3. [Build Prosedürü](#build-prosedürü)
4. [Database Setup](#database-setup)
5. [Üretim Deployment](#üretim-deployment)
6. [Docker Deployment](#docker-deployment)
7. [Monitoring & Logging](#monitoring--logging)
8. [Troubleshooting](#troubleshooting)
9. [Rollback Prosedürü](#rollback-prosedürü)
10. [Checklist](#checklist)

---

## 🔧 Ön Koşullar

### Yazılım Gereksinimleri

| Yazılım | Versiyon | Durum |
|---------|----------|-------|
| .NET SDK | 9.0+ | ✅ Gerekli |
| MySQL Server | 8.0+ | ✅ Gerekli |
| Git | 2.30+ | ✅ Tavsiye |
| Visual Studio Code | Latest | ✅ Tavsiye |
| PowerShell | 5.1+ | ✅ Windows |

### Hardware Gereksinimleri

**Minimum**:
- CPU: 2 cores
- RAM: 4 GB
- Storage: 10 GB

**Önerilen**:
- CPU: 4 cores
- RAM: 8 GB
- Storage: 20 GB

### Port Gereksinimleri

| Port | Servis | Durum |
|------|--------|-------|
| **5010** | ASP.NET Core | ✅ Default |
| **3306** | MySQL | ✅ Default |
| **443** | HTTPS | ✅ Prod |
| **80** | HTTP | ⚠️ Geçici |

---

## 💻 Yerel Geliştirme

### Adım 1: Depo'yu Clone'la

```powershell
# HTTPS
git clone https://github.com/yourorg/gamabelmvc.git
cd gamabelmvc

# SSH (önerilir)
git clone git@github.com:yourorg/gamabelmvc.git
cd gamabelmvc
```

### Adım 2: Bağımlılıkları Kur

```powershell
# NuGet packages
dotnet restore

# Detaylı çıktı
dotnet restore --verbosity detailed
```

### Adım 3: Database'i Setup Et

```powershell
# SQL scripts'ini çalıştır
mysql -u root -p < sql/sqltasarim.sql
mysql -u root -p < sql/sikayet_schema.sql
mysql -u root -p < sql/migration_add_firma_field.sql

# Test user ekle
mysql -u root -p < insert_test_user.sql
```

### Adım 4: Konfigürasyonu Ayarla

**appsettings.Development.json**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=gamabel_mvc;User ID=root;Password=yourpassword;"
  },
  "AllowedHosts": "*"
}
```

### Adım 5: Uygulamayı Çalıştır

```powershell
# Dev-tools script'i kullan (önerilir)
.\dev-tools.ps1 -Action run -Port 5010

# Veya manual
dotnet run --configuration Development

# Browser'da aç
# http://localhost:5010
```

---

## 🔨 Build Prosedürü

### Debug Build (Geliştirme)

```powershell
# Clean + Build
dotnet clean
dotnet build --configuration Debug

# Çıktı
# ✅ Build succeeded (0 errors, 45 warnings)
# Output: bin/Debug/net9.0/
```

### Release Build (Üretim)

```powershell
# Clean + Build optimized
dotnet clean
dotnet build --configuration Release

# Çıktı
# ✅ Build succeeded (0 errors, 45 warnings)
# Output: bin/Release/net9.0/
```

### Publish (Deployment)

```powershell
# Self-contained Windows executable
dotnet publish -c Release -r win-x64 --self-contained -o publish

# Framework-dependent (önerilir - daha küçük)
dotnet publish -c Release -o publish

# Linux (Production)
dotnet publish -c Release -r linux-x64 --self-contained -o publish
```

### Build Hata Çözümü

#### Sorun: MSB3021 - File Lock

```powershell
# ❌ Hata
# Cannot overwrite file because it's locked

# ✅ Çözüm
Get-Process gamabelmvc -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet clean
dotnet build
```

#### Sorun: NuGet Paket

```powershell
# ❌ Hata
# Package not found

# ✅ Çözüm
dotnet nuget locals all --clear
dotnet restore --force
```

---

## 🗄️ Database Setup

### MySQL Kurulumu (Windows)

```powershell
# Kontrol et
mysql --version

# Yok ise:
# 1. mysql.com'dan indir
# 2. Setup wizard'ı çalıştır
# 3. Service'i başlat
Start-Service MySQL80
```

### Database Oluşturma

```sql
-- Admin olarak
CREATE DATABASE gamabel_mvc 
CHARACTER SET utf8mb4 
COLLATE utf8mb4_unicode_ci;

-- User oluştur
CREATE USER 'gamabel_user'@'localhost' 
IDENTIFIED BY 'secure_password_123';

-- İzin ver
GRANT ALL PRIVILEGES ON gamabel_mvc.* 
TO 'gamabel_user'@'localhost';

FLUSH PRIVILEGES;

-- Kontrol et
USE gamabel_mvc;
SHOW TABLES;
```

### Migrasyonlar

```powershell
# SQL script'lerini çalıştır (sırasıyla)
1. sql/sqltasarim.sql                        # Ana şema
2. sql/sikayet_schema.sql                    # Şikayet tabloları
3. sql/migration_add_firma_field.sql         # Ürün Firma alanı
4. sql/migration_add_silindi_status.sql      # Soft-delete (YENI - 10 Haziran)
5. sql/migration_add_admin_email.sql         # Admin email (YENI - 10 Haziran)
6. insert_test_user.sql                      # Test kullanıcıları

# ⚡ HIZLI TIP: Runtime migrations otomatik çalışıyor!
# İlk sayfa yüklenişinde aşağıdakiler otomatik oluşturulur:
# - stk_EksikKaydi.SilindiMi (soft-delete)
# - admin_kullanicilar.email (email config)
```

**Migration Detayları**:

#### migration_add_silindi_status.sql
```sql
-- Soft-delete özelliği için
ALTER TABLE stk_EksikKaydi 
MODIFY Durum ENUM(..., 'Silindi') NOT NULL;

ALTER TABLE stk_EksikKaydi
ADD COLUMN SilindiMi BOOLEAN DEFAULT FALSE,
ADD COLUMN SilmeSebebi VARCHAR(500) NULL,
ADD COLUMN SilmeTarihi DATETIME NULL,
ADD COLUMN SilenKullaniciId INT NULL,
ADD INDEX idx_silindi (SilindiMi);
```

#### migration_add_admin_email.sql
```sql
-- Admin email konfigürasyonu için
ALTER TABLE admin_kullanicilar
ADD COLUMN IF NOT EXISTS email VARCHAR(255) NULL 
COMMENT 'Admin kullanıcısının email adresi (raporlar için)';

ALTER TABLE admin_kullanicilar
ADD INDEX IF NOT EXISTS idx_email (email);
```

### Backup & Restore

```powershell
# Backup
mysqldump -u root -p gamabel_mvc > backup_$(Get-Date -Format yyyyMMdd).sql

# Restore
mysql -u root -p gamabel_mvc < backup_20260525.sql

# Otomatik backup (PowerShell scheduled task)
# Bkz: source-backup.ps1
```

---

## 🌐 Üretim Deployment

### Linux Server (Önerilir)

#### 1. Server Hazırlama

```bash
# Ubuntu 22.04 LTS
sudo apt update && sudo apt upgrade -y

# .NET Runtime
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --version latest --runtime aspnetcore

# MySQL
sudo apt install mysql-server -y
sudo mysql_secure_installation

# Nginx (Reverse Proxy)
sudo apt install nginx -y
```

#### 2. Uygulama Deploy

```bash
# Application folder
sudo mkdir -p /var/www/gamabelmvc
sudo chown $USER:$USER /var/www/gamabelmvc

# Publish et
cd /path/to/gamabelmvc
dotnet publish -c Release -o /var/www/gamabelmvc/publish

# Permissions
sudo chown -R www-data:www-data /var/www/gamabelmvc
sudo chmod -R 755 /var/www/gamabelmvc
```

#### 3. Systemd Service

**/etc/systemd/system/gamabelmvc.service**:
```ini
[Unit]
Description=Gamabel MVC Application
After=network.target

[Service]
Type=notify
User=www-data
WorkingDirectory=/var/www/gamabelmvc/publish
ExecStart=/usr/bin/dotnet /var/www/gamabelmvc/publish/gamabelmvc.dll
Restart=always
RestartSec=10
Environment="ASPNETCORE_ENVIRONMENT=Production"
Environment="ASPNETCORE_URLS=http://localhost:5010"

[Install]
WantedBy=multi-user.target
```

```bash
# Enable & Start
sudo systemctl daemon-reload
sudo systemctl enable gamabelmvc
sudo systemctl start gamabelmvc

# Status kontrol
sudo systemctl status gamabelmvc
```

#### 4. Nginx Configuration

**/etc/nginx/sites-available/gamabelmvc**:
```nginx
upstream dotnet_app {
    server 127.0.0.1:5010;
}

server {
    listen 80;
    listen [::]:80;
    server_name gamabel.example.com;

    # HTTP to HTTPS redirect
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name gamabel.example.com;

    # SSL certificates (Let's Encrypt)
    ssl_certificate /etc/letsencrypt/live/gamabel.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/gamabel.example.com/privkey.pem;

    # Reverse proxy
    location / {
        proxy_pass http://dotnet_app;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_buffer_size 128k;
        proxy_buffers 4 256k;
        proxy_busy_buffers_size 256k;
    }

    # Static files caching
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg)$ {
        expires 30d;
        add_header Cache-Control "public, immutable";
    }
}
```

```bash
# Enable site
sudo ln -s /etc/nginx/sites-available/gamabelmvc /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl restart nginx
```

#### 5. SSL Sertifikası (Let's Encrypt)

```bash
# Certbot kur
sudo apt install certbot python3-certbot-nginx -y

# Sertifika al
sudo certbot certonly --nginx -d gamabel.example.com

# Auto-renewal
sudo systemctl enable certbot.timer
sudo systemctl start certbot.timer
```

### Windows Server

```powershell
# IIS'de host et
# 1. Application Pool oluştur
# 2. Published files'ı upload et
# 3. Website oluştur ve App Pool'u assign et
# 4. Binding'leri konfigure et (HTTP/HTTPS)

# Veya command line
$physicalPath = "C:\inetpub\gamabelmvc"
$appPoolName = "gamabelmvc-pool"

# Application Pool
New-WebAppPool -Name $appPoolName -Force
Set-ItemProperty -Path IIS:\AppPools\$appPoolName -Name processModel.identityType -Value "NetworkService"

# Web Site
New-WebSite -Name "gamabelmvc" `
    -Port 80 `
    -PhysicalPath $physicalPath `
    -ApplicationPool $appPoolName `
    -Force

Start-WebSite -Name "gamabelmvc"
```

---

## 🐳 Docker Deployment

### Dockerfile

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=publish /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=1 \
    CMD curl -f http://localhost:5010/health || exit 1

EXPOSE 5010
ENTRYPOINT ["dotnet", "gamabelmvc.dll"]
```

### docker-compose.yml

```yaml
version: '3.8'

services:
  app:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: gamabelmvc
    ports:
      - "5010:5010"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Server=mysql;Database=gamabel_mvc;User ID=root;Password=rootpassword;
    depends_on:
      - mysql
    networks:
      - gamabel-network
    restart: always

  mysql:
    image: mysql:8.0
    container_name: gamabel_mysql
    environment:
      MYSQL_ROOT_PASSWORD: rootpassword
      MYSQL_DATABASE: gamabel_mvc
    ports:
      - "3306:3306"
    volumes:
      - mysql_data:/var/lib/mysql
      - ./sql:/docker-entrypoint-initdb.d
    networks:
      - gamabel-network
    restart: always

  nginx:
    image: nginx:latest
    container_name: gamabel_nginx
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf
      - ./certs:/etc/nginx/certs
    depends_on:
      - app
    networks:
      - gamabel-network
    restart: always

volumes:
  mysql_data:

networks:
  gamabel-network:
    driver: bridge
```

```bash
# Build ve çalıştır
docker-compose up -d

# Logs
docker-compose logs -f app

# Stop
docker-compose down
```

---

## 📊 Monitoring & Logging

### Application Logging

```csharp
// Program.cs'de setup
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddDebug();
    
    // Serilog (önerilir)
    logging.AddSerilog(new LoggerConfiguration()
        .WriteTo.Console()
        .WriteTo.File("logs/application-.txt", 
            rollingInterval: RollingInterval.Day)
        .CreateLogger());
});
```

### Health Check Endpoint

```csharp
// Program.cs'de
app.MapHealthChecks("/health");

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>()
    .AddCheck("Database", () =>
    {
        // Database connection check
        return HealthCheckResult.Healthy();
    });
```

### Performance Monitoring

```csharp
// Application Insights
builder.Services.AddApplicationInsightsTelemetry();

// Custom metrics
var telemetryClient = new TelemetryClient();
telemetryClient.TrackEvent("SevkiyatEvent");
```

---

## 🔧 Troubleshooting

### Application Errors

#### 500 Internal Server Error

```
1. Logs kontrol et
   - /var/log/gamabelmvc/ (Linux)
   - Event Viewer (Windows)

2. Database connection test
   mysql -u gamabel_user -p gamabel_mvc -e "SELECT 1;"

3. Konfigürasyonu kontrol et
   - appsettings.json
   - Connection string
   - Ports
```

#### Database Connection Error

```csharp
// Test code
var connectionString = "Server=localhost;Database=gamabel_mvc;User ID=root;Password=pass;";
using (var connection = new MySqlConnection(connectionString))
{
    try
    {
        connection.Open();
        Console.WriteLine("✅ Connection successful");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error: {ex.Message}");
    }
}
```

#### Port Already in Use

```powershell
# Windows
netstat -ano | findstr :5010
taskkill /PID <PID> /F

# Linux
lsof -i :5010
kill -9 <PID>
```

---

## ↩️ Rollback Prosedürü

### Dosya-Based Rollback

```bash
# Backup (production)
sudo cp -r /var/www/gamabelmvc /var/www/gamabelmvc.backup.$(date +%Y%m%d)

# Rollback
sudo cp -r /var/www/gamabelmvc.backup.20260525/* /var/www/gamabelmvc/

# Service restart
sudo systemctl restart gamabelmvc
```

### Database Rollback

```sql
-- Backup from date
RESTORE DATABASE gamabel_mvc 
FROM DISK = '/backups/gamabel_mvc_20260525.sql';
```

### Git-Based Rollback

```bash
# Last commit'e dön
git revert HEAD

# Specific version'a dön
git checkout v3.0

# Redeploy
dotnet publish -c Release -o /var/www/gamabelmvc/publish
sudo systemctl restart gamabelmvc
```

---

## ✅ Checklist

### Pre-Deployment

- [ ] Build successful (0 errors)
- [ ] Database migrations applied
- [ ] appsettings.json configured
- [ ] Security patches applied
- [ ] Unit tests passed (geliştiriliyoruz)
- [ ] Code review completed
- [ ] Documentation updated

### Deployment

- [ ] Server prepared (OS, .NET, MySQL)
- [ ] Application deployed
- [ ] Configuration files set
- [ ] SSL certificates installed
- [ ] Database backup created
- [ ] Services started
- [ ] Health checks passing

### Post-Deployment

- [ ] Application accessible
- [ ] Database connection working
- [ ] User authentication verified
- [ ] Core features tested
- [ ] Performance acceptable
- [ ] Logs monitored
- [ ] Backup scheduled

### First Week Monitoring

- [ ] Daily health checks
- [ ] Error logs reviewed
- [ ] Performance metrics
- [ ] User feedback collected
- [ ] Security scans run
- [ ] Database backup verified

---

## 📞 Support

**Deployment Issues**: DevOps Team  
**Application Errors**: Development Team  
**Database Problems**: Database Admin  
**Security Issues**: Security Team

---

**Son Güncelleme**: 25 Mayıs 2026  
**Versiyon**: 1.0  
**Durum**: ✅ Hazır

**İlgili Dosyalar**:
- README.md
- TECHNICAL_DOCUMENTATION.md
- PROJECT_ANALYSIS_TR.md
