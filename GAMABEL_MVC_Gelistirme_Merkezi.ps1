# ============================================================
# GAMABEL MVC - GELISTIRME YONETIM MERKEZI
# ============================================================
# Ozellikler:
# 1  - Uygulamayi calistir
# 2  - Projeyi build et
# 3  - Testleri calistir
# 4  - Git durumunu goster
# 5  - GitHub guncellemelerini lokale uygula
# 6  - Lokal degisiklikleri GitHub'a yukle
# 7  - GitHub durumunu kontrol et
# 8  - Akilli senkronizasyon
# 9  - Degisiklikleri detayli incele
# 10 - Proje yedegi al
# 11 - Son yedegi geri yukle
# 12 - VS Code'da projeyi ac
# 13 - Git conflict durumunu kontrol et
# 14 - GitHub commit gecmisini goster
# 0  - Cikis
#
# Guvenlik:
# - GitHub'daki yeni commitler push oncesi kontrol edilir.
# - Lokal degisikliklerin uzerine zorla yazilmaz.
# - Pull islemi --ff-only kullanir.
# - Push oncesi Build/Test secenegi vardir.
# - Riskli dosya/klasorler icin uyari verilir.
# - Pull oncesi istege bagli otomatik yedek alinir.
# ============================================================

$ErrorActionPreference = "Continue"

# ------------------------------------------------------------
# GENEL AYARLAR
# ------------------------------------------------------------

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

$backupRoot = Join-Path $scriptPath "_GAMABEL_BACKUPS"

# Git'e gonderilmesi istenmeyen / kontrol edilmesi gereken dosyalar
$RiskliDosyaPatternleri = @(
    "appsettings.json",
    "appsettings.*.json",
    "*.log",
    "*.bak",
    "*.sql",
    "*.zip",
    "*.rar",
    "*.7z",
    ".env",
    ".env.*",
    "*password*",
    "*secret*",
    "*token*"
)

# Git tarafinda genellikle repoya alinmamasi gereken klasorler
$RiskliKlasorPatternleri = @(
    "\bin\",
    "\obj\",
    "\.vs\",
    "\node_modules\",
    "\_GAMABEL_BACKUPS\"
)

# ------------------------------------------------------------
# YARDIMCI FONKSIYONLAR
# ------------------------------------------------------------

function Pause-Menu {
    Write-Host ""
    Read-Host "Devam etmek icin Enter'a basin" | Out-Null
}


function ConvertTo-LatinChars {
    param([string]$Metin)

    if ([string]::IsNullOrWhiteSpace($Metin)) {
        return "Guncelleme"
    }

    $Sonuc = $Metin
    $Sonuc = $Sonuc -replace 'ç', 'c'
    $Sonuc = $Sonuc -replace 'Ç', 'C'
    $Sonuc = $Sonuc -replace 'ğ', 'g'
    $Sonuc = $Sonuc -replace 'Ğ', 'G'
    $Sonuc = $Sonuc -replace 'ı', 'i'
    $Sonuc = $Sonuc -replace 'İ', 'I'
    $Sonuc = $Sonuc -replace 'ö', 'o'
    $Sonuc = $Sonuc -replace 'Ö', 'O'
    $Sonuc = $Sonuc -replace 'ş', 's'
    $Sonuc = $Sonuc -replace 'Ş', 'S'
    $Sonuc = $Sonuc -replace 'ü', 'u'
    $Sonuc = $Sonuc -replace 'Ü', 'U'
    $Sonuc = $Sonuc -replace '[^a-zA-Z0-9\s\.\-_]', ''
    $Sonuc = $Sonuc -replace '\s+', ' '
    $Sonuc = $Sonuc.Trim()

    if ([string]::IsNullOrWhiteSpace($Sonuc)) {
        return "Guncelleme"
    }

    return $Sonuc
}

function Test-Git {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        Write-Host ""
        Write-Host "Git kurulu degil!" -ForegroundColor Red
        return $false
    }

    return $true
}

function Test-DotNet {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Host ""
        Write-Host "dotnet bulunamadi!" -ForegroundColor Red
        return $false
    }

    return $true
}

function Test-GitRepository {
    git rev-parse --git-dir *> $null

    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "Bu klasor bir Git deposu degil!" -ForegroundColor Red
        return $false
    }

    return $true
}

function Test-Internet {
    try {
        Invoke-WebRequest "https://github.com" -UseBasicParsing -TimeoutSec 10 | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

function Get-CurrentBranch {
    $Branch = git branch --show-current 2>$null

    if ([string]::IsNullOrWhiteSpace($Branch)) {
        return "(Detached HEAD)"
    }

    return $Branch.Trim()
}

function Get-RemoteUrl {
    try {
        $Remote = git remote get-url origin 2>$null
        return $Remote.Trim()
    }
    catch {
        return ""
    }
}

function Get-ProjectFiles {
    return @(Get-ChildItem -Path $scriptPath -Recurse -Filter "*.csproj" -File -ErrorAction SilentlyContinue |
        Where-Object {
            $_.FullName -notlike "$backupRoot*"
        })
}

function Get-ProjectFile {
    $Projects = @(Get-ProjectFiles)

    if ($Projects.Count -eq 0) {
        Write-Host "Calistirilacak .csproj bulunamadi." -ForegroundColor Red
        return $null
    }

    if ($Projects.Count -eq 1) {
        return $Projects[0]
    }

    Write-Host ""
    Write-Host "Birden fazla .csproj bulundu:" -ForegroundColor Yellow

    for ($i = 0; $i -lt $Projects.Count; $i++) {
        Write-Host "[$($i + 1)] $($Projects[$i].FullName)"
    }

    $Secim = Read-Host "Calistirilacak projeyi secin"

    if ($Secim -match '^\d+$') {
        $Index = [int]$Secim - 1

        if ($Index -ge 0 -and $Index -lt $Projects.Count) {
            return $Projects[$Index]
        }
    }

    Write-Host "Gecersiz secim." -ForegroundColor Red
    return $null
}

function Show-Header {
    Clear-Host

    $Branch = Get-CurrentBranch
    $Remote = Get-RemoteUrl

    Write-Host ""
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host "                 GAMABEL MVC GELISTIRME MERKEZI                    " -ForegroundColor Yellow
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Proje : $scriptPath" -ForegroundColor Magenta
    Write-Host "Branch: $Branch" -ForegroundColor Green

    if (-not [string]::IsNullOrWhiteSpace($Remote)) {
        Write-Host "GitHub: $Remote" -ForegroundColor Gray
    }

    Write-Host ""
}

function Get-GitCounts {
    $Branch = Get-CurrentBranch

    if ($Branch -eq "(Detached HEAD)") {
        return @{
            Behind = 0
            Ahead = 0
        }
    }

    $Behind = git rev-list --count "HEAD..origin/$Branch" 2>$null
    $Ahead = git rev-list --count "origin/$Branch..HEAD" 2>$null

    if ([string]::IsNullOrWhiteSpace($Behind)) { $Behind = 0 }
    if ([string]::IsNullOrWhiteSpace($Ahead)) { $Ahead = 0 }

    return @{
        Behind = [int]$Behind
        Ahead = [int]$Ahead
    }
}

function Get-LocalChanges {
    return @(git status --porcelain)
}

function Show-LocalChanges {
    $Changes = @(Get-LocalChanges)

    if ($Changes.Count -eq 0) {
        Write-Host "Lokal degisiklik yok." -ForegroundColor Green
        return
    }

    Write-Host ""
    Write-Host "LOKAL DEGISIKLIKLER" -ForegroundColor Cyan
    Write-Host "------------------------------------------------------------"

    git status --short

    Write-Host ""
    Write-Host "Toplam: $($Changes.Count) degisiklik" -ForegroundColor Yellow
}

function Get-StatusCodePath {
    param([string]$Line)

    if ($Line.Length -gt 3) {
        return $Line.Substring(3)
    }

    return $Line
}

function Test-RiskyPath {
    param([string]$RelativePath)

    $Normalized = $RelativePath.Replace("/", "\")

    foreach ($Pattern in $RiskliKlasorPatternleri) {
        if ($Normalized -like "*$Pattern*") {
            return $true
        }
    }

    $Name = Split-Path $Normalized -Leaf

    foreach ($Pattern in $RiskliDosyaPatternleri) {
        if ($Name -like $Pattern) {
            return $true
        }
    }

    return $false
}

# ------------------------------------------------------------
# 1 - UYGULAMAYI CALISTIR
# ------------------------------------------------------------

function Start-Application {
    Write-Host ""
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host "                         UYGULAMAYI CALISTIR                        " -ForegroundColor Yellow
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host ""

    if (-not (Test-DotNet)) {
        Pause-Menu
        return
    }

    $Project = Get-ProjectFile

    if ($null -eq $Project) {
        Pause-Menu
        return
    }

    Write-Host "Proje: $($Project.FullName)" -ForegroundColor Green
    Write-Host ""
    Write-Host "dotnet run baslatiliyor..." -ForegroundColor Yellow
    Write-Host ""


    dotnet run --project $Project.FullName

    Write-Host ""
    Write-Host "Uygulama sonlandi." -ForegroundColor Yellow
    Pause-Menu
}

# ------------------------------------------------------------
# 2 - BUILD
# ------------------------------------------------------------

function Build-Application {
    Write-Host ""
    Write-Host ">> PROJE BUILD" -ForegroundColor Cyan
    Write-Host ""

    if (-not (Test-DotNet)) {
        Pause-Menu
        return
    }

    $Project = Get-ProjectFile

    if ($null -eq $Project) {
        Pause-Menu
        return
    }

    dotnet build $Project.FullName

    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "BUILD BASARILI." -ForegroundColor Green
    }
    else {
        Write-Host ""
        Write-Host "BUILD BASARISIZ." -ForegroundColor Red
    }

    Pause-Menu
}

# ------------------------------------------------------------
# 3 - TEST
# ------------------------------------------------------------

function Test-Application {
    Write-Host ""
    Write-Host ">> TESTLER" -ForegroundColor Cyan
    Write-Host ""

    if (-not (Test-DotNet)) {
        Pause-Menu
        return
    }

    $TestProjects = @(Get-ChildItem -Path $scriptPath -Recurse -Filter "*.csproj" -File -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -match "Test" -and
            $_.FullName -notlike "$backupRoot*"
        })

    if ($TestProjects.Count -eq 0) {
        Write-Host "Test projesi bulunamadi." -ForegroundColor Yellow
        Pause-Menu
        return
    }

    dotnet test

    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "TESTLER BASARILI." -ForegroundColor Green
    }
    else {
        Write-Host ""
        Write-Host "TESTLER BASARISIZ." -ForegroundColor Red
    }

    Pause-Menu
}

# ------------------------------------------------------------
# 4 - GIT DURUMU
# ------------------------------------------------------------

function Show-GitStatus {
    Write-Host ""
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host "                           GIT DURUMU                              " -ForegroundColor Yellow
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host ""

    git status

    Write-Host ""
    Write-Host "SON COMMIT" -ForegroundColor Cyan
    git log -1 --oneline

    Pause-Menu
}

# ------------------------------------------------------------
# 5 - GITHUB -> LOKAL
# ------------------------------------------------------------

function Pull-FromGitHub {
    Write-Host ""
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host "              GITHUB GUNCELLEMELERINI LOKALE UYGULA                " -ForegroundColor Yellow
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host ""

    if (-not (Test-Internet)) {
        Write-Host "Internet baglantisi yok." -ForegroundColor Red
        Pause-Menu
        return
    }

    $Branch = Get-CurrentBranch

    if ($Branch -eq "(Detached HEAD)") {
        Write-Host "Detached HEAD durumundasiniz." -ForegroundColor Red
        Pause-Menu
        return
    }

    Write-Host "GitHub bilgileri aliniyor..." -ForegroundColor Yellow
    git fetch origin

    if ($LASTEXITCODE -ne 0) {
        Write-Host "GitHub'dan bilgiler alinamadi." -ForegroundColor Red
        Pause-Menu
        return
    }

    $Counts = Get-GitCounts
    $Changes = @(Get-LocalChanges)

    Write-Host ""
    Write-Host "GitHub'da yeni commit : $($Counts.Behind)" -ForegroundColor Yellow
    Write-Host "Lokal yeni commit     : $($Counts.Ahead)" -ForegroundColor Yellow
    Write-Host "Lokal dosya degisikligi: $($Changes.Count)" -ForegroundColor Yellow

    if ($Counts.Behind -eq 0) {
        Write-Host ""
        Write-Host "GitHub'da uygulanacak yeni guncelleme yok." -ForegroundColor Green
        Pause-Menu
        return
    }

    if ($Changes.Count -gt 0) {
        Write-Host ""
        Write-Host "UYARI: Lokal bilgisayarda commit edilmemis degisiklikler var!" -ForegroundColor Red
        Show-LocalChanges
        Write-Host ""
        Write-Host "Guvenli yontem: once degisikliklerinizi GitHub'a yukleyin veya stash/yedek alin." -ForegroundColor Yellow

        $Yedek = Read-Host "Pull oncesi otomatik yedek almak ister misiniz? (e/h)"

        if ($Yedek -eq "e" -or $Yedek -eq "E") {
            if (-not (New-ProjectBackup)) {
                Write-Host "Yedek basarisiz. Pull islemi iptal edildi." -ForegroundColor Red
                Pause-Menu
                return
            }
        }

        Write-Host ""
        $Devam = Read-Host "Lokal degisiklikler varken devam etmek istiyor musunuz? (e/h)"

        if ($Devam -ne "e" -and $Devam -ne "E") {
            Write-Host "Pull islemi iptal edildi." -ForegroundColor Yellow
            Pause-Menu
            return
        }
    }

    Write-Host ""
    Write-Host "GitHub'daki yeni commitler:" -ForegroundColor Cyan
    git --no-pager log --oneline "HEAD..origin/$Branch"

    Write-Host ""
    $Onay = Read-Host "Bu guncellemeleri lokal bilgisayara uygulamak istiyor musunuz? (e/h)"

    if ($Onay -ne "e" -and $Onay -ne "E") {
        Write-Host "Pull islemi iptal edildi." -ForegroundColor Yellow
        Pause-Menu
        return
    }

    Write-Host ""
    Write-Host "Guncellemeler uygulanıyor..." -ForegroundColor Yellow

    # --ff-only: conflict yaratacak otomatik merge yapmaz.
    git pull --ff-only origin $Branch

    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "GITHUB GUNCELLEMELERI LOKALE BASARIYLA UYGULANDI." -ForegroundColor Green
    }
    else {
        Write-Host ""
        Write-Host "PULL BASARISIZ." -ForegroundColor Red
        Write-Host "Lokal ve GitHub arasinda birlestirme gerektiren fark olabilir." -ForegroundColor Yellow
        Write-Host "Mevcut kod zorla ezilmedi." -ForegroundColor Green
    }

    Pause-Menu
}

# ------------------------------------------------------------
# 6 - LOKAL -> GITHUB
# ------------------------------------------------------------

function Push-ToGitHub {
    Write-Host ""
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host "              LOKAL DEGISIKLIKLERI GITHUB'A YUKLE                  " -ForegroundColor Yellow
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host ""

    if (-not (Test-Internet)) {
        Write-Host "Internet baglantisi yok." -ForegroundColor Red
        Pause-Menu
        return
    }

    $Branch = Get-CurrentBranch

    if ($Branch -eq "(Detached HEAD)") {
        Write-Host "Detached HEAD durumundasiniz." -ForegroundColor Red
        Pause-Menu
        return
    }

    git fetch origin

    if ($LASTEXITCODE -ne 0) {
        Write-Host "GitHub bilgileri alinamadi." -ForegroundColor Red
        Pause-Menu
        return
    }

    $Counts = Get-GitCounts
    $Changes = @(Get-LocalChanges)

    Write-Host "GitHub'dan geride     : $($Counts.Behind)" -ForegroundColor Yellow
    Write-Host "GitHub'a gonderilecek : $($Changes.Count) dosya degisikligi" -ForegroundColor Yellow

    # GitHub'da yeni commit varsa push'u engelle.
    if ($Counts.Behind -gt 0) {
        Write-Host ""
        Write-Host "UYARI: GitHub'da sizde olmayan yeni commitler var!" -ForegroundColor Red
        Write-Host ""
        git --no-pager log --oneline "HEAD..origin/$Branch"
        Write-Host ""
        Write-Host "Once 5 - GitHub Guncellemelerini Lokale Uygula secenegini kullanin." -ForegroundColor Yellow
        Pause-Menu
        return
    }

    if ($Changes.Count -eq 0) {
        Write-Host ""
        Write-Host "GitHub'a gonderilecek lokal degisiklik yok." -ForegroundColor Green
        Pause-Menu
        return
    }

    Show-LocalChanges

    # Riskli dosya kontrolu
    $RiskliDosyalar = @()

    foreach ($Line in $Changes) {
        $Path = Get-StatusCodePath $Line

        if (Test-RiskyPath $Path) {
            $RiskliDosyalar += $Path
        }
    }

    if ($RiskliDosyalar.Count -gt 0) {
        Write-Host ""
        Write-Host "============================================================" -ForegroundColor Red
        Write-Host "                  RISKLI DOSYA UYARISI                      " -ForegroundColor Red
        Write-Host "============================================================" -ForegroundColor Red
        Write-Host ""

        foreach ($Dosya in $RiskliDosyalar) {
            Write-Host "  ! $Dosya" -ForegroundColor Red
        }

        Write-Host ""
        Write-Host "Bu dosyalar log, yedek, ayar veya gizli bilgi icerebilir." -ForegroundColor Yellow
        Write-Host "Bunlar .gitignore ile disarida tutulmalidir." -ForegroundColor Yellow
        Write-Host ""

        $RiskOnay = Read-Host "Yine de devam etmek istiyor musunuz? (e/h)"

        if ($RiskOnay -ne "e" -and $RiskOnay -ne "E") {
            Write-Host "Push islemi iptal edildi." -ForegroundColor Yellow
            Pause-Menu
            return
        }
    }

    Write-Host ""
    $Onay = Read-Host "Bu degisiklikleri GitHub'a yuklemek istiyor musunuz? (e/h)"

    if ($Onay -ne "e" -and $Onay -ne "E") {
        Write-Host "Push islemi iptal edildi." -ForegroundColor Yellow
        Pause-Menu
        return
    }

    Write-Host ""
    Write-Host "git add . calistiriliyor..." -ForegroundColor Yellow
    git add .

    if ($LASTEXITCODE -ne 0) {
        Write-Host "git add basarisiz." -ForegroundColor Red
        Pause-Menu
        return
    }

    Write-Host ""
    Write-Host "STAGE EDILEN DEGISIKLIKLER" -ForegroundColor Cyan
    git --no-pager diff --cached --stat

    Write-Host ""
    $Detay = Read-Host "Tam degisiklikleri gormek ister misiniz? (e/h)"

    if ($Detay -eq "e" -or $Detay -eq "E") {
        Write-Host ""
        git --no-pager diff --cached
        Write-Host ""
    }

    # Build
    if (Test-DotNet) {
        $BuildOnay = Read-Host "Commit oncesi dotnet build calistirilsin mi? (e/h)"

        if ($BuildOnay -eq "e" -or $BuildOnay -eq "E") {
            $Project = Get-ProjectFile

            if ($null -eq $Project) {
                Write-Host "csproj bulunamadi. Build atlaniyor." -ForegroundColor Yellow
            }
            else {
                Write-Host ""
                Write-Host "BUILD CALISTIRILIYOR..." -ForegroundColor Yellow

                dotnet build $Project.FullName

                if ($LASTEXITCODE -ne 0) {
                    Write-Host ""
                    Write-Host "BUILD BASARISIZ." -ForegroundColor Red
                    Write-Host "Commit islemi iptal edildi." -ForegroundColor Yellow
                    Write-Host "Stage edilen dosyalar korunuyor." -ForegroundColor Green
                    Pause-Menu
                    return
                }

                Write-Host "BUILD BASARILI." -ForegroundColor Green
            }
        }
    }

    # Test
    $TestProjects = @(Get-ChildItem -Path $scriptPath -Recurse -Filter "*.csproj" -File -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -match "Test" -and
            $_.FullName -notlike "$backupRoot*"
        })

    if ($TestProjects.Count -gt 0 -and (Test-DotNet)) {
        $TestOnay = Read-Host "Commit oncesi testler calistirilsin mi? (e/h)"

        if ($TestOnay -eq "e" -or $TestOnay -eq "E") {
            Write-Host ""
            Write-Host "TESTLER CALISTIRILIYOR..." -ForegroundColor Yellow

            dotnet test

            if ($LASTEXITCODE -ne 0) {
                Write-Host ""
                Write-Host "TESTLER BASARISIZ." -ForegroundColor Red
                Write-Host "Commit islemi iptal edildi." -ForegroundColor Yellow
                Pause-Menu
                return
            }

            Write-Host "TESTLER BASARILI." -ForegroundColor Green
        }
    }

    do {
        Write-Host ""
        $Mesaj = Read-Host "Commit mesajini yazin"

        if ([string]::IsNullOrWhiteSpace($Mesaj)) {
            Write-Host "Commit mesaji bos olamaz." -ForegroundColor Red
        }
    }
    while ([string]::IsNullOrWhiteSpace($Mesaj))

    $MesajLatin = ConvertTo-LatinChars $Mesaj

    Write-Host ""
    Write-Host "Commit mesaji: $MesajLatin" -ForegroundColor Yellow

    $CommitOnay = Read-Host "Commit yapilsin mi? (e/h)"

    if ($CommitOnay -ne "e" -and $CommitOnay -ne "E") {
        Write-Host "Commit islemi iptal edildi." -ForegroundColor Yellow
        Pause-Menu
        return
    }

    git commit -m "$MesajLatin"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Commit basarisiz." -ForegroundColor Red
        Pause-Menu
        return
    }

    Write-Host ""
    Write-Host "COMMIT BASARILI." -ForegroundColor Green

    Write-Host ""
    Write-Host "GONDERILECEK COMMIT" -ForegroundColor Cyan
    git --no-pager show --stat --oneline HEAD

    # Committen sonra tekrar fetch.
    # Bu sayede kullanici beklerken GitHub'a yeni commit gelmisse push edilmez.
    Write-Host ""
    Write-Host "Push oncesi GitHub tekrar kontrol ediliyor..." -ForegroundColor Yellow

    git fetch origin

    if ($LASTEXITCODE -ne 0) {
        Write-Host "GitHub kontrolu basarisiz. Push yapilmadi." -ForegroundColor Red
        Write-Host "Commit lokal bilgisayarda kayitli." -ForegroundColor Yellow
        Pause-Menu
        return
    }

    $CountsAfterCommit = Get-GitCounts

    if ($CountsAfterCommit.Behind -gt 0) {
        Write-Host ""
        Write-Host "DURUM DEGISTI: GitHub'a yeni commit gelmis!" -ForegroundColor Red
        Write-Host "Guvenlik nedeniyle push yapilmadi." -ForegroundColor Yellow
        Write-Host "Once GitHub'daki degisiklikleri lokale uygulayin." -ForegroundColor Yellow
        Pause-Menu
        return
    }

    Write-Host ""
    $PushOnay = Read-Host "Commit GitHub'a push edilsin mi? (e/h)"

    if ($PushOnay -ne "e" -and $PushOnay -ne "E") {
        Write-Host "Push islemi yapilmadi." -ForegroundColor Yellow
        Write-Host "Commit lokal bilgisayarda kayitli." -ForegroundColor Green
        Pause-Menu
        return
    }

    git push origin $Branch

    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "DEGISIKLIKLER GITHUB'A BASARIYLA YUKLENDI." -ForegroundColor Green
    }
    else {
        Write-Host ""
        Write-Host "PUSH BASARISIZ." -ForegroundColor Red
        Write-Host "Commit lokal bilgisayarda kayitli." -ForegroundColor Yellow
        Write-Host "Daha sonra tekrar deneyebilirsiniz." -ForegroundColor Yellow
    }

    Pause-Menu
}

# ------------------------------------------------------------
# 7 - GITHUB DURUMU
# ------------------------------------------------------------

function Check-GitHubStatus {
    Write-Host ""
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host "                         GITHUB DURUMU                             " -ForegroundColor Yellow
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host ""

    if (-not (Test-Internet)) {
        Write-Host "Internet baglantisi yok." -ForegroundColor Red
        Pause-Menu
        return
    }

    $Branch = Get-CurrentBranch

    if ($Branch -eq "(Detached HEAD)") {
        Write-Host "Detached HEAD durumundasiniz." -ForegroundColor Red
        Pause-Menu
        return
    }

    git fetch origin

    if ($LASTEXITCODE -ne 0) {
        Write-Host "GitHub bilgileri alinamadi." -ForegroundColor Red
        Pause-Menu
        return
    }

    $Counts = Get-GitCounts
    $Changes = @(Get-LocalChanges)

    Write-Host "Branch                      : $Branch" -ForegroundColor White
    Write-Host "GitHub'dan geride           : $($Counts.Behind) commit" -ForegroundColor Yellow
    Write-Host "GitHub'a gonderilmemis      : $($Counts.Ahead) commit" -ForegroundColor Yellow
    Write-Host "Commit edilmemis dosya      : $($Changes.Count)" -ForegroundColor Yellow
    Write-Host ""

    if ($Counts.Behind -eq 0 -and $Counts.Ahead -eq 0 -and $Changes.Count -eq 0) {
        Write-Host "LOKAL VE GITHUB TAM SENKRON." -ForegroundColor Green
    }
    elseif ($Counts.Behind -gt 0 -and $Counts.Ahead -eq 0) {
        Write-Host "GITHUB'DA YENI GUNCELLEMELER VAR." -ForegroundColor Yellow
        Write-Host "5. secenek ile lokale uygulayabilirsiniz." -ForegroundColor Yellow
    }
    elseif ($Counts.Behind -eq 0 -and $Counts.Ahead -gt 0) {
        Write-Host "LOKALDE GITHUB'A GONDERILMEMIS COMMITLER VAR." -ForegroundColor Yellow
        Write-Host "6. secenek ile GitHub'a yukleyebilirsiniz." -ForegroundColor Yellow
    }
    elseif ($Counts.Behind -gt 0 -and $Counts.Ahead -gt 0) {
        Write-Host "UYARI: LOKAL VE GITHUB AYRISMIS." -ForegroundColor Red
        Write-Host "Her iki tarafta da farkli commitler var." -ForegroundColor Red
        Write-Host "Akilli senkronizasyon secenegini kullanin." -ForegroundColor Yellow
    }
    elseif ($Changes.Count -gt 0) {
        Write-Host "LOKALDE COMMIT EDILMEMIS DEGISIKLIKLER VAR." -ForegroundColor Yellow
    }

    Write-Host ""
    Show-LocalChanges

    if ($Counts.Behind -gt 0) {
        Write-Host ""
        Write-Host "GITHUB'DAKI YENI COMMITLER:" -ForegroundColor Cyan
        git --no-pager log --oneline "HEAD..origin/$Branch"
    }

    if ($Counts.Ahead -gt 0) {
        Write-Host ""
        Write-Host "LOKALDEKI GONDERILMEMIS COMMITLER:" -ForegroundColor Cyan
        git --no-pager log --oneline "origin/$Branch..HEAD"
    }

    Pause-Menu
}

# ------------------------------------------------------------
# 8 - AKILLI SENKRONIZASYON
# ------------------------------------------------------------

function Smart-Sync {
    Write-Host ""
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host "                       AKILLI SENKRONIZASYON                        " -ForegroundColor Yellow
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host ""

    if (-not (Test-Internet)) {
        Write-Host "Internet baglantisi yok." -ForegroundColor Red
        Pause-Menu
        return
    }

    $Branch = Get-CurrentBranch

    if ($Branch -eq "(Detached HEAD)") {
        Write-Host "Detached HEAD durumundasiniz." -ForegroundColor Red
        Pause-Menu
        return
    }

    Write-Host "GitHub kontrol ediliyor..." -ForegroundColor Yellow

    git fetch origin

    if ($LASTEXITCODE -ne 0) {
        Write-Host "GitHub bilgileri alinamadi." -ForegroundColor Red
        Pause-Menu
        return
    }

    $Counts = Get-GitCounts
    $Changes = @(Get-LocalChanges)

    Write-Host ""
    Write-Host "GitHub'dan geride : $($Counts.Behind)" -ForegroundColor Yellow
    Write-Host "Lokal ileri       : $($Counts.Ahead)" -ForegroundColor Yellow
    Write-Host "Lokal degisiklik  : $($Changes.Count)" -ForegroundColor Yellow
    Write-Host ""

    # Durum 1: Tam senkron
    if ($Counts.Behind -eq 0 -and $Counts.Ahead -eq 0 -and $Changes.Count -eq 0) {
        Write-Host "SISTEM TAM SENKRON." -ForegroundColor Green
        Pause-Menu
        return
    }

    # Durum 2: Sadece GitHub'da yeni commit
    if ($Counts.Behind -gt 0 -and $Counts.Ahead -eq 0 -and $Changes.Count -eq 0) {
        Write-Host "GitHub'da yeni guncellemeler bulundu." -ForegroundColor Yellow
        git --no-pager log --oneline "HEAD..origin/$Branch"
        Write-Host ""

        $Onay = Read-Host "GitHub guncellemeleri lokale uygulansin mi? (e/h)"

        if ($Onay -eq "e" -or $Onay -eq "E") {
            git pull --ff-only origin $Branch

            if ($LASTEXITCODE -eq 0) {
                Write-Host "Senkronizasyon tamamlandi." -ForegroundColor Green
            }
            else {
                Write-Host "Pull basarisiz." -ForegroundColor Red
            }
        }

        Pause-Menu
        return
    }

    # Durum 3: Sadece lokal commit
    if ($Counts.Behind -eq 0 -and $Counts.Ahead -gt 0 -and $Changes.Count -eq 0) {
        Write-Host "Lokal commitler GitHub'a gonderilmemis." -ForegroundColor Yellow
        git --no-pager log --oneline "origin/$Branch..HEAD"
        Write-Host ""

        $Onay = Read-Host "Lokal commitler GitHub'a push edilsin mi? (e/h)"

        if ($Onay -eq "e" -or $Onay -eq "E") {
            git push origin $Branch

            if ($LASTEXITCODE -eq 0) {
                Write-Host "Senkronizasyon tamamlandi." -ForegroundColor Green
            }
            else {
                Write-Host "Push basarisiz." -ForegroundColor Red
            }
        }

        Pause-Menu
        return
    }

    # Durum 4: Sadece lokal commit edilmemis degisiklik
    if ($Counts.Behind -eq 0 -and $Counts.Ahead -eq 0 -and $Changes.Count -gt 0) {
        Write-Host "Lokal commit edilmemis degisiklikler var." -ForegroundColor Yellow
        Show-LocalChanges
        Write-Host ""
        Write-Host "6. secenek ile GitHub'a yukleyebilirsiniz." -ForegroundColor Green
        Pause-Menu
        return
    }

    # Durum 5: Hem GitHub hem lokal commit var
    if ($Counts.Behind -gt 0 -and $Counts.Ahead -gt 0) {
        Write-Host "KRITIK DURUM: Lokal ve GitHub ayrismis." -ForegroundColor Red
        Write-Host ""
        Write-Host "GitHub'daki commitler:" -ForegroundColor Cyan
        git --no-pager log --oneline "HEAD..origin/$Branch"
        Write-Host ""
        Write-Host "Lokal commitler:" -ForegroundColor Cyan
        git --no-pager log --oneline "origin/$Branch..HEAD"
        Write-Host ""
        Write-Host "Otomatik merge yapilmadi." -ForegroundColor Green
        Write-Host "Bu durumda manuel conflict kontrolu gereklidir." -ForegroundColor Yellow
        Pause-Menu
        return
    }

    # Durum 6: GitHub yeni + lokal commit edilmemis dosyalar
    if ($Counts.Behind -gt 0 -and $Changes.Count -gt 0) {
        Write-Host "UYARI: GitHub'da yeni kod VE lokal degisiklik var." -ForegroundColor Red
        Write-Host ""
        Show-LocalChanges
        Write-Host ""
        Write-Host "Guvenli yol:" -ForegroundColor Cyan
        Write-Host "1. Lokal degisiklikleri yedekle/commit et"
        Write-Host "2. GitHub guncellemelerini al"
        Write-Host "3. Gerekirse conflict coz"
        Write-Host "4. Test et"
        Write-Host "5. GitHub'a gonder"
        Pause-Menu
        return
    }

    Pause-Menu
}

# ------------------------------------------------------------
# 9 - DETAYLI DEGISIKLIKLER
# ------------------------------------------------------------

function Show-DetailedChanges {
    Write-Host ""
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host "                    DEGISIKLIKLERI INCELE                          " -ForegroundColor Yellow
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host ""

    $Changes = @(Get-LocalChanges)

    if ($Changes.Count -eq 0) {
        Write-Host "Degisiklik yok." -ForegroundColor Green
        Pause-Menu
        return
    }

    git --no-pager diff --stat

    Write-Host ""
    $Onay = Read-Host "Tam degisiklikleri gormek ister misiniz? (e/h)"

    if ($Onay -eq "e" -or $Onay -eq "E") {
        git --no-pager diff
    }

    Pause-Menu
}

# ------------------------------------------------------------
# 10 - PROJE YEDEGI
# ------------------------------------------------------------

function New-ProjectBackup {
    try {
        if (-not (Test-Path $backupRoot)) {
            New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
        }

        $Stamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
        $BackupPath = Join-Path $backupRoot $Stamp

        New-Item -ItemType Directory -Path $BackupPath -Force | Out-Null

        Write-Host ""
        Write-Host "Proje yedegi aliniyor..." -ForegroundColor Yellow

        # Git repository icindeki calisma dosyalarini yedekler.
        # .git klasoru kopyalanmaz.
        $Items = Get-ChildItem -Path $scriptPath -Force |
            Where-Object {
                $_.Name -ne ".git" -and
                $_.Name -ne "_GAMABEL_BACKUPS"
            }

        foreach ($Item in $Items) {
            Copy-Item -Path $Item.FullName -Destination $BackupPath -Recurse -Force -ErrorAction Stop
        }

        Write-Host ""
        Write-Host "YEDEK BASARIYLA ALINDI:" -ForegroundColor Green
        Write-Host $BackupPath -ForegroundColor Gray


        return $true
    }
    catch {
        Write-Host ""
        Write-Host "YEDEK ALINAMADI: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Backup-Menu {
    Write-Host ""
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host "                         PROJE YEDEGI                              " -ForegroundColor Yellow
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host ""

    New-ProjectBackup | Out-Null

    Pause-Menu
}

# ------------------------------------------------------------
# 11 - SON YEDEGI GERI YUKLE
# ------------------------------------------------------------

function Restore-LatestBackup {
    Write-Host ""
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host "                      SON YEDEGI GERI YUKLE                         " -ForegroundColor Yellow
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host ""

    if (-not (Test-Path $backupRoot)) {
        Write-Host "Yedek klasoru bulunamadi." -ForegroundColor Yellow
        Pause-Menu
        return
    }

    $Backups = @(Get-ChildItem -Path $backupRoot -Directory | Sort-Object Name -Descending)

    if ($Backups.Count -eq 0) {
        Write-Host "Yedek bulunamadi." -ForegroundColor Yellow
        Pause-Menu
        return
    }

    $Latest = $Backups[0]

    Write-Host "Son yedek:" -ForegroundColor Cyan
    Write-Host $Latest.FullName -ForegroundColor Gray
    Write-Host ""

    Write-Host "DIKKAT: Mevcut proje dosyalarinin uzerine yazilabilir." -ForegroundColor Red
    $Onay = Read-Host "Geri yukleme yapmak istiyor musunuz? (GERI YUKLE yazin)"

    if ($Onay -ne "GERI YUKLE") {
        Write-Host "Geri yukleme iptal edildi." -ForegroundColor Yellow
        Pause-Menu
        return
    }

    try {
        $Items = Get-ChildItem -Path $Latest.FullName -Force

        foreach ($Item in $Items) {
            Copy-Item -Path $Item.FullName -Destination $scriptPath -Recurse -Force -ErrorAction Stop
        }

        Write-Host ""
        Write-Host "SON YEDEK GERI YUKLENDI." -ForegroundColor Green
    }
    catch {
        Write-Host ""
        Write-Host "Geri yukleme basarisiz: $($_.Exception.Message)" -ForegroundColor Red
    }

    Pause-Menu
}

# ------------------------------------------------------------
# 12 - VS CODE'DA AC
# ------------------------------------------------------------

function Open-VSCode {
    Write-Host ""
    Write-Host ">> VS CODE ACILIYOR..." -ForegroundColor Cyan
    Write-Host ""

    if (Get-Command code -ErrorAction SilentlyContinue) {
        code $scriptPath
        Write-Host "VS Code acildi." -ForegroundColor Green
    }
    else {
        Write-Host "VS Code 'code' komutu bulunamadi." -ForegroundColor Red
        Write-Host "VS Code PATH ayarini kontrol edin." -ForegroundColor Yellow
    }

    Pause-Menu
}

# ------------------------------------------------------------
# 13 - CONFLICT KONTROLU
# ------------------------------------------------------------

function Check-Conflicts {
    Write-Host ""
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host "                         CONFLICT KONTROL                          " -ForegroundColor Yellow
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host ""

    $Conflicts = @(git diff --name-only --diff-filter=U)

    if ($Conflicts.Count -eq 0) {
        Write-Host "Conflict bulunmuyor." -ForegroundColor Green
        Pause-Menu
        return
    }

    Write-Host "CONFLICT BULUNDU!" -ForegroundColor Red
    Write-Host ""

    foreach ($File in $Conflicts) {
        Write-Host "  $File" -ForegroundColor Red
    }

    Write-Host ""
    Write-Host "Bu dosyalardaki <<<<<<< ======= >>>>>>> bolumlerini kontrol edin." -ForegroundColor Yellow

    if (Get-Command code -ErrorAction SilentlyContinue) {
        Write-Host ""
        $Open = Read-Host "Conflict dosyalarini VS Code'da acmak ister misiniz? (e/h)"

        if ($Open -eq "e" -or $Open -eq "E") {
            foreach ($File in $Conflicts) {
                code $File
            }
        }
    }

    Pause-Menu
}

# ------------------------------------------------------------
# 14 - COMMIT GECMISI
# ------------------------------------------------------------

function Show-CommitHistory {
    Write-Host ""
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host "                        COMMIT GECMISI                             " -ForegroundColor Yellow
    Write-Host "====================================================================" -ForegroundColor Cyan
    Write-Host ""

    git --no-pager log --graph --decorate --oneline --all -30

    Pause-Menu
}

# ------------------------------------------------------------
# ANA MENU
# ------------------------------------------------------------

if (-not (Test-Git)) {
    Pause-Menu
    exit 1
}

if (-not (Test-GitRepository)) {
    Pause-Menu
    exit 1
}


while ($true) {

    Show-Header

    Write-Host "  [1]  Uygulamayi Calistir" -ForegroundColor White
    Write-Host "  [2]  Projeyi Build Et" -ForegroundColor White
    Write-Host "  [3]  Testleri Calistir" -ForegroundColor White
    Write-Host "  [4]  Git Durumunu Goster" -ForegroundColor White
    Write-Host ""
    Write-Host "  [5]  GitHub Guncellemelerini Lokale Uygula" -ForegroundColor Green
    Write-Host "  [6]  Yaptigim Degisiklikleri GitHub'a Yukle" -ForegroundColor Green
    Write-Host "  [7]  GitHub Durumunu Kontrol Et" -ForegroundColor White
    Write-Host "  [8]  AKILLI SENKRONIZASYON" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  [9]  Degisiklikleri Detayli Incele" -ForegroundColor White
    Write-Host "  [10] Proje Yedegi Al" -ForegroundColor White
    Write-Host "  [11] Son Yedegi Geri Yukle" -ForegroundColor White
    Write-Host "  [12] VS Code'da Projeyi Ac" -ForegroundColor White
    Write-Host "  [13] Git Conflict Kontrolu" -ForegroundColor White
    Write-Host "  [14] Commit Gecmisini Goster" -ForegroundColor White
    Write-Host ""
    Write-Host "  [0]  Cikis" -ForegroundColor Red
    Write-Host ""

    $Secim = Read-Host "Seciminiz"

    switch ($Secim) {

        "1"  { Start-Application }
        "2"  { Build-Application }
        "3"  { Test-Application }
        "4"  { Show-GitStatus }
        "5"  { Pull-FromGitHub }
        "6"  { Push-ToGitHub }
        "7"  { Check-GitHubStatus }
        "8"  { Smart-Sync }
        "9"  { Show-DetailedChanges }
        "10" { Backup-Menu }
        "11" { Restore-LatestBackup }
        "12" { Open-VSCode }
        "13" { Check-Conflicts }
        "14" { Show-CommitHistory }

        "0" {
            Clear-Host
            Write-Host ""
            Write-Host "GAMABEL MVC Gelistirme Merkezi kapatildi." -ForegroundColor Green
            Write-Host ""
            exit 0
        }

        default {
            Write-Host ""
            Write-Host "Gecersiz secim." -ForegroundColor Red
            Start-Sleep -Seconds 1
        }
    }
}
