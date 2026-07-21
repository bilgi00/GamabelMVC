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

# Değişiklik olan dosyaları göster
$status = git status --porcelain

if ([string]::IsNullOrWhiteSpace($status)) {
    Write-Host ""
    Write-Host "No changes found." -ForegroundColor Yellow
    Read-Host "Press Enter to continue..."
    exit
}

# =============================================
# DEĞİŞİKLİK OLAN DOSYALARI LİSTELE
# =============================================
Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "     CHANGED FILES TO BE COMMITTED     " -ForegroundColor Yellow
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Dosyaları durumlarıyla birlikte listele
$files = git status --porcelain
$modifiedCount = 0
$addedCount = 0
$deletedCount = 0

foreach ($file in $files) {
    $statusCode = $file.Substring(0, 2)
    $fileName = $file.Substring(3)
    
    switch ($statusCode) {
        'M ' { 
            Write-Host "  📝 MODIFIED:  $fileName" -ForegroundColor Yellow
            $modifiedCount++
        }
        'A ' { 
            Write-Host "  ➕ ADDED:     $fileName" -ForegroundColor Green
            $addedCount++
        }
        'D ' { 
            Write-Host "  ❌ DELETED:   $fileName" -ForegroundColor Red
            $deletedCount++
        }
        'R ' { 
            Write-Host "  🔄 RENAMED:   $fileName" -ForegroundColor Cyan
            $modifiedCount++
        }
        '??' { 
            Write-Host "  ❓ UNTRACKED: $fileName" -ForegroundColor Gray
            $addedCount++
        }
        default { 
            Write-Host "  $statusCode  $fileName" -ForegroundColor Gray
        }
    }
}

Write-Host ""
Write-Host "--------------------------------------------------" -ForegroundColor DarkGray
Write-Host "  TOTAL: $($files.Count) files changed" -ForegroundColor White
Write-Host "  + Added: $addedCount  |  ✏️ Modified: $modifiedCount  |  - Deleted: $deletedCount" -ForegroundColor White
Write-Host "--------------------------------------------------" -ForegroundColor DarkGray
Write-Host ""

# Değişiklik özetini göster (opsiyonel)
$showDetails = Read-Host "Show detailed changes? (y/n)"
if ($showDetails -eq 'y' -or $showDetails -eq 'Y' -or $showDetails -eq 'e' -or $showDetails -eq 'E') {
    Write-Host ""
    Write-Host ">> Detailed changes:" -ForegroundColor Cyan
    git diff --cached --stat
    Write-Host ""
}

# Commit mesajı al
do {
    Write-Host ""
    $mesaj = Read-Host "Enter commit message (cannot be empty)"
    
    if ([string]::IsNullOrWhiteSpace($mesaj)) {
        Write-Host "Commit message cannot be empty!" -ForegroundColor Red
    }
} while ([string]::IsNullOrWhiteSpace($mesaj))

# Türkçe karakterleri ASCII'ye dönüştür
$mesajLatin = ConvertTo-LatinChars -text $mesaj

Write-Host ""
Write-Host ">> Converted message: $mesajLatin" -ForegroundColor Yellow

Write-Host ""
Write-Host ">> Committing..." -ForegroundColor Green
git commit -m "$mesajLatin"

# Commit başarılı mı kontrol et
if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host ">> Pushing to GitHub..." -ForegroundColor Green
    
    # Push öncesi gönderilecek dosyaları göster
    Write-Host ""
    Write-Host "Files to be pushed:" -ForegroundColor Cyan
    git show --stat --oneline HEAD
    Write-Host ""
    
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