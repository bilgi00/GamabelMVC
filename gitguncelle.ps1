# =============================================
# TÜRKÇE KARAKTER DÖNÜŞTÜRÜCÜ (ASCII)
# =============================================
function ConvertTo-LatinChars {
    param([string]$text)
    
    # ASCII'ye dönüştür
    $bytes = [System.Text.Encoding]::GetEncoding("Cyrillic").GetBytes($text)
    return [System.Text.Encoding]::ASCII.GetString($bytes)
}

# =============================================
Clear-Host

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "      GAMABEL MVC GIT UPDATE          " -ForegroundColor Yellow
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Proje klasörü - Script'in bulunduğu dizini al
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

Write-Host ">> Project Directory: $scriptPath" -ForegroundColor Magenta
Write-Host ""

# Önce son değişiklikleri al
Write-Host ">> Pulling latest changes from GitHub..." -ForegroundColor Green
git pull

Write-Host ""
Write-Host "Press Enter when code changes are complete..." -ForegroundColor Yellow
Read-Host

Write-Host ""
Write-Host ">> Adding changes..." -ForegroundColor Green
git add .

$status = git status --porcelain

if ([string]::IsNullOrWhiteSpace($status)) {
    Write-Host ""
    Write-Host "No changes found." -ForegroundColor Yellow
    Read-Host "Press Enter to continue..."
    exit
}

# Commit mesajı al - boş girilirse tekrar sor
do {
    Write-Host ""
    $mesaj = Read-Host "Enter commit message (cannot be empty)"
    
    if ([string]::IsNullOrWhiteSpace($mesaj)) {
        Write-Host "Commit message cannot be empty!" -ForegroundColor Red
    }
} while ([string]::IsNullOrWhiteSpace($mesaj))

# Türkçe karakterleri ASCII'ye dönüştür
$mesajLatin = ConvertTo-LatinChars -text $mesaj

# Dönüştürülmüş mesajı göster
Write-Host ""
Write-Host ">> Converted message: $mesajLatin" -ForegroundColor Yellow

Write-Host ""
Write-Host ">> Committing..." -ForegroundColor Green
git commit -m "$mesajLatin"

# Commit başarılı mı kontrol et
if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host ">> Pushing to GitHub..." -ForegroundColor Green
    git push
} else {
    Write-Host ""
    Write-Host "Commit failed!" -ForegroundColor Red
    Read-Host "Press Enter to continue..."
    exit
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "         OPERATION COMPLETED          " -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

Read-Host "Press Enter to continue..."