Clear-Host

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "      GAMABEL MVC GIT GUNCELLEME      " -ForegroundColor Yellow
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Proje klasörü
Set-Location "D:\GamabelMVC"

# Önce son değişiklikleri al
Write-Host ">> GitHub'dan son değişiklikler alınıyor..." -ForegroundColor Green
git pull

Write-Host ""
Write-Host "Kod düzenlemelerini tamamladıysan Enter'a bas..." -ForegroundColor Yellow
Pause

Write-Host ""
Write-Host ">> Değişiklikler ekleniyor..." -ForegroundColor Green
git add .

$status = git status --porcelain

if ([string]::IsNullOrWhiteSpace($status)) {

    Write-Host ""
    Write-Host "Değişiklik bulunamadı." -ForegroundColor Yellow
    Pause
    exit

}

Write-Host ""
$mesaj = Read-Host "Commit mesajını yaz"

git commit -m "$mesaj"

Write-Host ""
Write-Host ">> GitHub'a gönderiliyor..." -ForegroundColor Green

git push

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "        ISLEM BASARIYLA BITTI         " -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

Pause