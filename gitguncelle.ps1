# =============================================
# GAMABEL MVC GIT GUNCELLEME
# =============================================

# =============================================
# 1. GIT KURULU MU KONTROL ET
# =============================================
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host "Git kurulu degil!" -ForegroundColor Red
    Read-Host "Devam etmek icin Enter'a basin..."
    exit 1
}

# =============================================
# 2. GIT DEPOSU KONTROL ET
# =============================================
git rev-parse --git-dir *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Bu klasor Git deposu degil!" -ForegroundColor Red
    Read-Host "Devam etmek icin Enter'a basin..."
    exit 1
}

# =============================================
# 3. TURKCE KARAKTER DONUSTURUCU (ASCII)
# =============================================
function ConvertTo-LatinChars {
    param([string]$metin)
    
    if ([string]::IsNullOrWhiteSpace($metin)) {
        return "Guncelleme"
    }
    
    $sonuc = $metin
    $sonuc = $sonuc -replace 'ç', 'c'
    $sonuc = $sonuc -replace 'Ç', 'C'
    $sonuc = $sonuc -replace 'ğ', 'g'
    $sonuc = $sonuc -replace 'Ğ', 'G'
    $sonuc = $sonuc -replace 'ı', 'i'
    $sonuc = $sonuc -replace 'İ', 'I'
    $sonuc = $sonuc -replace 'ö', 'o'
    $sonuc = $sonuc -replace 'Ö', 'O'
    $sonuc = $sonuc -replace 'ş', 's'
    $sonuc = $sonuc -replace 'Ş', 'S'
    $sonuc = $sonuc -replace 'ü', 'u'
    $sonuc = $sonuc -replace 'Ü', 'U'
    
    $sonuc = $sonuc -replace '[^a-zA-Z0-9\s\.\-_]', ''
    $sonuc = $sonuc -replace '\s+', ' '
    $sonuc = $sonuc.Trim()
    
    if ([string]::IsNullOrWhiteSpace($sonuc)) {
        return "Guncelleme"
    }
    
    return $sonuc
}

# =============================================
# 4. INTERNET BAGLANTI KONTROL (HTTP)
# =============================================
function Test-InternetConnection {
    try {
        Invoke-WebRequest https://github.com -UseBasicParsing -TimeoutSec 10 | Out-Null
        return $true
    } catch {
        return $false
    }
}

# =============================================
# 5. ANA MENU
# =============================================
Clear-Host
Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "      GAMABEL MVC GIT GUNCELLEME      " -ForegroundColor Yellow
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

$baslangicZamani = Get-Date

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

Write-Host ">> Proje Dizini: $scriptPath" -ForegroundColor Magenta
Write-Host ""

# =============================================
# 6. BRANCH BILGISI
# =============================================
$currentBranch = git branch --show-current
if ([string]::IsNullOrWhiteSpace($currentBranch)) {
    $currentBranch = "(Detached HEAD)"
}

try {
    $lastCommit = git log -1 --oneline
} catch {
    $lastCommit = "Henuz commit yok"
}

Write-Host "Guncel Dal: $currentBranch" -ForegroundColor Yellow
Write-Host "Son Kayit: $lastCommit" -ForegroundColor Yellow
Write-Host ""

# =============================================
# 7. INTERNET BAGLANTI KONTROL
# =============================================
Write-Host ">> Internet baglantisi kontrol ediliyor..." -ForegroundColor Yellow
if (-not (Test-InternetConnection)) {
    Write-Host "Internet baglantisi yok! Gonderme islemi iptal edildi." -ForegroundColor Red
    Read-Host "Devam etmek icin Enter'a basin..."
    exit 1
}
Write-Host "Internet baglantisi var" -ForegroundColor Green
Write-Host ""

# =============================================
# 8. GIT PULL
# =============================================
Write-Host ">> GitHub'dan son degisiklikler aliniyor..." -ForegroundColor Green
git pull

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Git Pull basarisiz!" -ForegroundColor Red
    Read-Host "Devam etmek icin Enter'a basin..."
    exit 1
}
Write-Host "Git Pull tamamlandi" -ForegroundColor Green
Write-Host ""

Write-Host "Kod duzenlemelerini tamamladiysan Enter'a bas..." -ForegroundColor Yellow
Read-Host

# =============================================
# 9. DEGISIKLIKLERI EKLE
# =============================================
Write-Host ""
Write-Host ">> Degisiklikler ekleniyor..." -ForegroundColor Green
git add .

# =============================================
# 10. DEGISIKLIKLERI LISTELE
# =============================================
$dosyalar = git status --porcelain

if ([string]::IsNullOrWhiteSpace($dosyalar)) {
    Write-Host ""
    Write-Host "Degisiklik bulunamadi." -ForegroundColor Yellow
    Read-Host "Devam etmek icin Enter'a basin..."
    exit 0
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "     KAYDEDILECEK DEGISIKLIKLER        " -ForegroundColor Yellow
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

$dosyaListesi = @($dosyalar)
$degisenSayisi = 0
$eklenenSayisi = 0
$silinenSayisi = 0

foreach ($dosya in $dosyaListesi) {
    $durumKodu = $dosya.Substring(0, 2)
    $dosyaAdi = $dosya.Substring(3)
    
    if ($durumKodu.Contains('M')) { 
        Write-Host "  DEGISTI:    $dosyaAdi" -ForegroundColor Yellow
        $degisenSayisi++
    }
    elseif ($durumKodu.Contains('A') -or $durumKodu.StartsWith('?')) { 
        Write-Host "  EKLENDI:   $dosyaAdi" -ForegroundColor Green
        $eklenenSayisi++
    }
    elseif ($durumKodu.Contains('D')) { 
        Write-Host "  SILINDI:   $dosyaAdi" -ForegroundColor Red
        $silinenSayisi++
    }
    elseif ($durumKodu.StartsWith('R')) { 
        Write-Host "  YENIDEN ADLANDIRILDI: $dosyaAdi" -ForegroundColor Cyan
        $degisenSayisi++
    }
    else { 
        Write-Host "  $durumKodu  $dosyaAdi" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "--------------------------------------------------" -ForegroundColor DarkGray
Write-Host "  TOPLAM: $($dosyaListesi.Count) dosya degisti" -ForegroundColor White
Write-Host "  + Eklenen: $eklenenSayisi  |  Degistirilen: $degisenSayisi  |  - Silinen: $silinenSayisi" -ForegroundColor White
Write-Host "--------------------------------------------------" -ForegroundColor DarkGray
Write-Host ""

# =============================================
# 11. TEST CALISTIR
# =============================================
$testCalistir = Read-Host "Kayit oncesi testleri calistirmak ister misiniz? (e/h)"
if ($testCalistir -eq 'e' -or $testCalistir -eq 'E') {
    Write-Host ""
    Write-Host ">> Testler calistiriliyor..." -ForegroundColor Yellow
    
    $testProjeleri = Get-ChildItem -Recurse *.csproj | Where-Object { $_.Name -match "Test" }
    if ($testProjeleri.Count -gt 0) {
        dotnet test
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Testler basarisiz! Kayit islemi iptal edildi." -ForegroundColor Red
            Read-Host "Devam etmek icin Enter'a basin..."
            exit 1
        }
        Write-Host "Testler basariyla gecti!" -ForegroundColor Green
    } else {
        Write-Host "Test projesi bulunamadi, atlaniyor..." -ForegroundColor Yellow
    }
}

# =============================================
# 12. DEGISIKLIK DETAYLARI
# =============================================
$detayGoster = Read-Host "Degisiklik detaylarini goster? (e/h)"
if ($detayGoster -eq 'e' -or $detayGoster -eq 'E') {
    Write-Host ""
    Write-Host ">> Degisiklik detaylari:" -ForegroundColor Cyan
    git --no-pager diff --cached --stat
    Write-Host ""
    
    $detayliGoster = Read-Host "Tam degisiklikleri goster? (e/h)"
    if ($detayliGoster -eq 'e' -or $detayliGoster -eq 'E') {
        Write-Host ""
        Write-Host ">> Tam degisiklikler:" -ForegroundColor Cyan
        git --no-pager diff --cached
        Write-Host ""
    }
}

# =============================================
# 13. COMMIT MESAJI AL
# =============================================
do {
    Write-Host ""
    $mesaj = Read-Host "Commit mesajini yaz (bos olamaz)"
    
    if ([string]::IsNullOrWhiteSpace($mesaj)) {
        Write-Host "Commit mesaji bos olamaz!" -ForegroundColor Red
    }
} while ([string]::IsNullOrWhiteSpace($mesaj))

$mesajLatin = ConvertTo-LatinChars -metin $mesaj
$mesajLatin = $mesajLatin.Trim()
$mesajLatin = $mesajLatin -replace '\s+', ' '

Write-Host ""
Write-Host ">> Donusturulen mesaj: $mesajLatin" -ForegroundColor Yellow

# =============================================
# 14. GIT COMMIT
# =============================================
Write-Host ""
Write-Host ">> Kaydediliyor..." -ForegroundColor Green
git commit -m "$mesajLatin"

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Kaydetme basarisiz!" -ForegroundColor Red
    Read-Host "Devam etmek icin Enter'a basin..."
    exit 1
}
Write-Host "Kaydetme basarili" -ForegroundColor Green

# =============================================
# 15. GIT PUSH
# =============================================
Write-Host ""
Write-Host ">> GitHub'a gonderiliyor..." -ForegroundColor Green

Write-Host ""
Write-Host "Gonderilecek dosyalar:" -ForegroundColor Cyan
git --no-pager show --stat --oneline HEAD
Write-Host ""

git push

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "GitHub'a gonderilemedi!" -ForegroundColor Red
    Write-Host "Degisiklikler yerel olarak kaydedildi (commit)." -ForegroundColor Yellow
    Write-Host "Manuel push yapabilirsiniz: git push" -ForegroundColor Yellow
    Read-Host "Devam etmek icin Enter'a basin..."
    exit 1
}
Write-Host "GitHub'a gonderildi" -ForegroundColor Green

# =============================================
# 16. LOG KAYDET
# =============================================
$logDosyasi = "$scriptPath\git_guncelleme_log.txt"
$bitisZamani = Get-Date
$gecenSure = ($bitisZamani - $baslangicZamani).TotalSeconds
$logKaydi = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] KAYIT: $mesajLatin | Dosya: $($dosyaListesi.Count) | Dal: $currentBranch | Sure: $([math]::Round($gecenSure, 2)) sn"
Add-Content -Path $logDosyasi -Value $logKaydi

# =============================================
# 17. OZET EKRANI
# =============================================
Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "         ISLEM BASARIYLA TAMAMLANDI   " -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "OZET" -ForegroundColor Yellow
Write-Host "  Dal: $currentBranch" -ForegroundColor White
Write-Host "  Dosya: $($dosyaListesi.Count) degisti" -ForegroundColor White
Write-Host "  Commit: $mesajLatin" -ForegroundColor White
Write-Host "  Sure: $([math]::Round($gecenSure, 2)) saniye" -ForegroundColor White
Write-Host "  Log: $logDosyasi" -ForegroundColor Gray
Write-Host ""

Read-Host "Devam etmek icin Enter'a basin..."