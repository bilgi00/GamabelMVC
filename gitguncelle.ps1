Clear-Host

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "      GAMABEL MVC GIT GUNCELLEME      " -ForegroundColor Yellow
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Proje klasörü - Script'in bulunduğu dizini al
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

# Proje dizinini göster
Write-Host ">> Proje Dizini: $scriptPath" -ForegroundColor Magenta
Write-Host ""

# Önce son değişiklikleri al
Write-Host ">> GitHub'dan son değişiklikler alınıyor..." -ForegroundColor Green
git pull

Write-Host ""
Write-Host "Kod düzenlemelerini tamamladıysan Enter'a bas..." -ForegroundColor Yellow
Read-Host

Write-Host ""
Write-Host ">> Değişiklikler ekleniyor..." -ForegroundColor Green
git add .

$status = git status --porcelain

if ([string]::IsNullOrWhiteSpace($status)) {
    Write-Host ""
    Write-Host "Değişiklik bulunamadı." -ForegroundColor Yellow
    Read-Host "Devam etmek için Enter'a bas..."
    exit
}

# Commit mesajı al - boş girilirse tekrar sor
do {
    Write-Host ""
    $mesaj = Read-Host "Commit mesajını yaz (boş bırakılamaz)"
    
    if ([string]::IsNullOrWhiteSpace($mesaj)) {
        Write-Host "Commit mesajı boş olamaz! Lütfen bir mesaj girin." -ForegroundColor Red
    }
} while ([string]::IsNullOrWhiteSpace($mesaj))

Write-Host ""
Write-Host ">> Commit yapılıyor..." -ForegroundColor Green
git commit -m "$mesaj"

# Commit başarılı mı kontrol et
if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host ">> GitHub'a gönderiliyor..." -ForegroundColor Green
    git push
} else {
    Write-Host ""
    Write-Host "Commit başarısız oldu!" -ForegroundColor Red
    Read-Host "Devam etmek için Enter'a bas..."
    exit
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "        ISLEM BASARIYLA BITTI         " -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

Read-Host "Devam etmek için Enter'a bas..."