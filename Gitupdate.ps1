#Requires -Version 5.1
# ============================================================
# GAMABEL MVC - GELISTIRME YONETIM MERKEZI  (v3 - birlesik surum)
# ============================================================
# Bu dosya uc scriptin tek dosyada birlesimidir:
#   gitupdate.ps1    (v2 menu)      -> temel alindi
#   runmenu.ps1      (v1 menu)      -> v2'nin ilk surumu; tum ozellikleri v2'de var
#   gitguncelle.ps1  (hizli akis)   -> menu 17 ve -Hizli parametresi olarak eklendi
#
# Kullanim:
#   .\Gitupdate.ps1                menuyu acar
#   .\Gitupdate.ps1 -Hizli         dogrudan "Hizli Guncelle" akisini calistirir
#
# Menu:
#  1  Uygulamayi calistir            9  Degisiklikleri detayli incele
#  2  Projeyi build et              10  Proje yedegi al
#  3  Testleri calistir             11  Yedekten geri yukle (secmeli)
#  4  Git durumunu goster           12  VS Code'da ac
#  5  GitHub -> Lokal (ff-only)     13  Conflict / yarim islem kontrolu
#  6  Lokal -> GitHub               14  Commit gecmisi
#  7  GitHub durumunu kontrol et    15  Branch islemleri
#  8  Akilli senkronizasyon         16  .gitignore denetimi
# 17  Hizli guncelle (pull > duzenle > commit > push)
# 18  Tum veritabanini yedekle
#  0  Cikis
#
# gitguncelle.ps1'den gelenler:
#  - Hizli akis (17): internet kontrolu, pull, "duzenlemeyi bitirince Enter",
#    commit, push (build/test, diff, risk ve secret taramasi menu 6 ile ortak)
#  - Renkli degisiklik listesi (EKLENDI / DEGISTI / SILINDI) ve toplamlar
#  - Islem logu: <repo>_git_guncelleme_log.txt (repo disinda, commit'e girmez)
#  - Islem sonu ozet ekrani (menu 6 ve 17 basarili push sonrasi)
#
# v2 ile gelen duzeltmeler:
#  - Yedek klasoru repo disinda; bin/obj/.vs/node_modules yedeklenmez
#  - Commit mesajlari UTF-8 (Turkce korunur); ASCII modu opsiyonel
#  - Sadece "ahead" commit varken push yapilabilir
#  - Upstream kontrolu, ayrismis (diverged) durum erken yakalanir
#  - Akilli senkronizasyon: stash tabanli guvenli pull, merge/rebase secenegi
#  - Riskli dosya + icerik bazli secret taramasi
#  - Geri yukleme oncesi guvenlik yedegi, yedek secimi, saklama politikasi
#  - Remote URL'de token maskeleme, PS 5.1 uyumlulugu
#
# Opsiyonel ayar dosyasi: script ile ayni klasorde gamabel.config.json
#   { "BackupKeep": 15, "DatabaseBackupKeep": 15, "AsciiCommitMessages": true }
# ============================================================

param(
    [switch]$Hizli
)

$ErrorActionPreference = "Continue"

try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.Encoding]::UTF8
}
catch {}

# ------------------------------------------------------------
# AYARLAR
# ------------------------------------------------------------

$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
Set-Location $ScriptDir

$Config = @{
    BackupKeep          = 10
    DatabaseBackupKeep  = 15
    AsciiCommitMessages = $false

    # Kesinlikle repoya girmemesi gereken dosya turleri
    RiskyFilePatterns   = @(
        "appsettings.Production.json", "appsettings.Staging.json", "appsettings.*.local.json",
        ".env", ".env.*",
        "*.log", "*.bak", "*.sql", "*.zip", "*.rar", "*.7z",
        "*.pfx", "*.key", "*.pem", "*.mdf", "*.ldf", "*.db", "*.sqlite"
    )

    # Repoya alinmamasi gereken klasorler (basinda ve sonunda \ olmali)
    RiskyFolderPatterns = @("\bin\", "\obj\", "\.vs\", "\node_modules\", "\_GAMABEL_BACKUPS\", "\packages\")

    # Ada bakarak uyari verilecek kelimeler (kod dosyalari haric)
    NameHintPatterns    = @("*password*", "*secret*", "*token*", "*credential*")

    # Ada gore uyari uretmeyecek kod uzantilari
    CodeExtensions      = @(".cs", ".cshtml", ".razor", ".vb", ".ts", ".js", ".css", ".html", ".md", ".resx")
}

$ConfigFile = Join-Path $ScriptDir "gamabel.config.json"
if (Test-Path $ConfigFile) {
    try {
        $Json = Get-Content $ConfigFile -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($Prop in $Json.PSObject.Properties) {
            if ($Config.ContainsKey($Prop.Name)) { $Config[$Prop.Name] = $Prop.Value }
        }
    }
    catch {
        Write-Host "gamabel.config.json okunamadi, varsayilanlar kullaniliyor." -ForegroundColor Yellow
    }
}

# Icerik taramasi (staged diff'teki eklenen satirlar)
$SecretRegex = [ordered]@{
    "Parola atamasi"          = '(?i)(password|passwd|pwd)\s*[=:]\s*["'']?[^\s"'';]{4,}'
    "Baglanti dizesi parolasi" = '(?i)(server|data source)=[^;]+;.*(password|pwd)='
    "Bearer token"            = '(?i)bearer\s+[a-z0-9\-_\.]{20,}'
    "API anahtari"            = '(?i)api[_-]?key\s*[=:]\s*["'']?[a-z0-9\-_]{16,}'
    "Secret atamasi"          = '(?i)(client_?secret|secret)\s*[=:]\s*["''][^"'']{8,}["'']'
    "GitHub token"            = 'gh[pousr]_[A-Za-z0-9]{30,}'
    "AWS anahtari"            = 'AKIA[0-9A-Z]{16}'
    "Ozel anahtar (PEM)"      = '-----BEGIN [A-Z ]*PRIVATE KEY-----'
}

# ------------------------------------------------------------
# GENEL YARDIMCILAR
# ------------------------------------------------------------

function Write-Ok   { param([string]$M) Write-Host $M -ForegroundColor Green }
function Write-Warn { param([string]$M) Write-Host $M -ForegroundColor Yellow }
function Write-Err  { param([string]$M) Write-Host $M -ForegroundColor Red }
function Write-Info { param([string]$M) Write-Host $M -ForegroundColor Cyan }

function Write-Banner {
    param([string]$Title)
    $Line = "=" * 68
    Write-Host ""
    Write-Host $Line -ForegroundColor Cyan
    Write-Host ("  " + $Title) -ForegroundColor Yellow
    Write-Host $Line -ForegroundColor Cyan
    Write-Host ""
}

function Wait-Menu {
    Write-Host ""
    Read-Host "Devam etmek icin Enter'a basin" | Out-Null
}


function Confirm-Action {
    param(
        [string]$Prompt,
        [bool]$Default = $false
    )

    $Suffix = if ($Default) { "(E/h)" } else { "(e/H)" }

    while ($true) {
        $Answer = (Read-Host "$Prompt $Suffix").Trim().ToLowerInvariant()

        if ($Answer -eq "") { return $Default }
        if ($Answer -in @("e", "evet", "y", "yes")) { return $true }
        if ($Answer -in @("h", "hayir", "n", "no")) { return $false }

        Write-Warn "Lutfen e veya h yazin."
    }
}

function ConvertTo-LatinChars {
    param([string]$Metin)

    if ([string]::IsNullOrWhiteSpace($Metin)) { return "Guncelleme" }

    # [char] kodlari kullanilir: dosya kodlamasindan bagimsiz, buyuk/kucuk harf duyarli.
    $Map = @{
        [char]0x00E7 = "c"; [char]0x00C7 = "C"
        [char]0x011F = "g"; [char]0x011E = "G"
        [char]0x0131 = "i"; [char]0x0130 = "I"
        [char]0x00F6 = "o"; [char]0x00D6 = "O"
        [char]0x015F = "s"; [char]0x015E = "S"
        [char]0x00FC = "u"; [char]0x00DC = "U"
    }

    $Sb = New-Object System.Text.StringBuilder
    foreach ($Ch in $Metin.ToCharArray()) {
        if ($Map.ContainsKey($Ch)) { [void]$Sb.Append($Map[$Ch]) }
        else { [void]$Sb.Append($Ch) }
    }

    $Sonuc = $Sb.ToString()
    $Sonuc = $Sonuc -replace '[^a-zA-Z0-9\s\.\-_]', ''
    $Sonuc = ($Sonuc -replace '\s+', ' ').Trim()

    if ([string]::IsNullOrWhiteSpace($Sonuc)) { return "Guncelleme" }
    return $Sonuc
}

function Test-Git {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        Write-Err "Git kurulu degil!"
        return $false
    }
    return $true
}

function Test-DotNet {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Err "dotnet bulunamadi!"
        return $false
    }
    return $true
}

function Test-GitRepository {
    git rev-parse --git-dir *> $null
    if ($LASTEXITCODE -ne 0) {
        Write-Err "Bu klasor bir Git deposu degil!"
        return $false
    }
    return $true
}

function Get-SafeRemoteUrl {
    try {
        $Remote = git remote get-url origin 2>$null
        if ([string]::IsNullOrWhiteSpace($Remote)) { return "" }
        # https://kullanici:TOKEN@github.com/... -> https://***@github.com/...
        return ("$Remote".Trim() -replace '//[^/@]+@', '//***@')
    }
    catch { return "" }
}

function Show-Header {
    Clear-Host

    $Info = Get-BranchInfo
    $Remote = Get-SafeRemoteUrl
    $ChangeCount = @(Get-LocalChanges).Count

    Write-Host ""
    Write-Host ("=" * 68) -ForegroundColor Cyan
    Write-Host "                 GAMABEL MVC GELISTIRME MERKEZI" -ForegroundColor Yellow
    Write-Host ("=" * 68) -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Proje : $script:RepoRoot" -ForegroundColor Magenta
    Write-Host "Branch: $($Info.Branch)" -ForegroundColor Green
    if ($Remote) { Write-Host "GitHub: $Remote" -ForegroundColor Gray }

    if ($Info.HasUpstream) {
        Write-Host ("Durum : Ileri {0} | Geri {1} | Degisiklik {2}   (son fetch'e gore)" -f $Info.Ahead, $Info.Behind, $ChangeCount) -ForegroundColor Gray
    }
    else {
        Write-Host ("Durum : Upstream yok | Degisiklik {0}" -f $ChangeCount) -ForegroundColor Gray
    }

    $Op = Get-OperationInProgress
    if ($Op) { Write-Err "UYARI: Yarim kalmis islem var ($Op). 13 numarali secenegi kullanin." }

    Write-Host ""
}

# ------------------------------------------------------------
# INTERNET / ISLEM LOGU / OZET  (gitguncelle.ps1'den)
# ------------------------------------------------------------

function Test-InternetConnection {
    try {
        Invoke-WebRequest "https://github.com" -UseBasicParsing -TimeoutSec 10 | Out-Null
        return $true
    }
    catch { return $false }
}

function Write-OperationLog {
    param(
        [string]$Message,
        [int]$FileCount,
        [string]$Branch,
        [double]$Seconds
    )

    try {
        $Path = if ($script:LogFile) { $script:LogFile } else { Join-Path $ScriptDir "git_guncelleme_log.txt" }
        $Line = "[{0}] KAYIT: {1} | Dosya: {2} | Dal: {3} | Sure: {4} sn" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Message, $FileCount, $Branch, $Seconds
        Add-Content -Path $Path -Value $Line -Encoding UTF8
        return $Path
    }
    catch {
        Write-Warn "Log yazilamadi: $($_.Exception.Message)"
        return $null
    }
}

# Basarili push sonrasi: log satiri yazar ve ozet ekranini gosterir.
function Complete-PushOperation {
    param(
        [string]$Message,
        [int]$FileCount,
        [string]$Branch,
        [datetime]$StartTime
    )

    $Seconds = [math]::Round(((Get-Date) - $StartTime).TotalSeconds, 2)
    $LogPath = Write-OperationLog -Message $Message -FileCount $FileCount -Branch $Branch -Seconds $Seconds

    Write-Host ""
    Write-Host ("=" * 38) -ForegroundColor Cyan
    Write-Host "         ISLEM BASARIYLA TAMAMLANDI" -ForegroundColor Green
    Write-Host ("=" * 38) -ForegroundColor Cyan
    Write-Host ""
    Write-Host "OZET" -ForegroundColor Yellow
    Write-Host "  Dal   : $Branch" -ForegroundColor White
    Write-Host "  Dosya : $FileCount degisti" -ForegroundColor White
    Write-Host "  Commit: $Message" -ForegroundColor White
    Write-Host "  Sure  : $Seconds saniye" -ForegroundColor White
    if ($LogPath) { Write-Host "  Log   : $LogPath" -ForegroundColor Gray }
}

# ------------------------------------------------------------
# GIT DURUM YARDIMCILARI
# ------------------------------------------------------------

function Get-BranchInfo {
    $Info = [ordered]@{
        Branch      = ""
        Detached    = $false
        HasUpstream = $false
        Upstream    = ""
        Ahead       = 0
        Behind      = 0
    }

    $B = git branch --show-current 2>$null

    if ([string]::IsNullOrWhiteSpace($B)) {
        $Info.Detached = $true
        $Info.Branch = "(Detached HEAD)"
        return [pscustomobject]$Info
    }

    $Info.Branch = "$B".Trim()

    $Up = git rev-parse --abbrev-ref --symbolic-full-name "@{u}" 2>$null
    if ($LASTEXITCODE -eq 0 -and $Up) {
        $Info.HasUpstream = $true
        $Info.Upstream = "$Up".Trim()

        $Counts = git rev-list --left-right --count "HEAD...@{u}" 2>$null
        if ($LASTEXITCODE -eq 0 -and $Counts) {
            $Parts = ("$Counts").Trim() -split '\s+'
            $Info.Ahead = [int]$Parts[0]
            $Info.Behind = [int]$Parts[1]
        }
    }

    return [pscustomobject]$Info
}

function Get-LocalChanges {
    $Lines = @(git -c core.quotepath=false status --porcelain=v1 -uall 2>$null)
    $Result = @()

    foreach ($L in $Lines) {
        if ([string]::IsNullOrEmpty($L) -or $L.Length -lt 4) { continue }

        $Code = $L.Substring(0, 2)
        $Path = $L.Substring(3)

        # Rename: "eski -> yeni"
        if ($Path -match ' -> ') { $Path = ($Path -split ' -> ')[-1] }

        # Bosluklu yollar tirnakli gelir
        if ($Path.Length -ge 2 -and $Path.StartsWith('"') -and $Path.EndsWith('"')) {
            $Path = $Path.Substring(1, $Path.Length - 2)
        }

        $First = $Code.Substring(0, 1)
        $Result += [pscustomobject]@{
            Code   = $Code
            Path   = $Path
            Staged = ($First -ne " " -and $First -ne "?")
        }
    }

    return , $Result
}

function Show-LocalChanges {
    $Changes = @(Get-LocalChanges)

    if ($Changes.Count -eq 0) {
        Write-Ok "Lokal degisiklik yok."
        return
    }

    Write-Info "LOKAL DEGISIKLIKLER"
    Write-Host ("-" * 60)

    $Eklenen = 0
    $Degisen = 0
    $Silinen = 0
    $Conflict = 0
    $MaxShown = 200
    $Shown = 0

    foreach ($C in $Changes) {
        $Code = $C.Code

        if ($Code.Contains("U") -or $Code -eq "AA" -or $Code -eq "DD") {
            $Label = "CONFLICT"; $Color = "Magenta"; $Conflict++
        }
        elseif ($Code -eq "??" -or $Code.Contains("A")) {
            $Label = "EKLENDI"; $Color = "Green"; $Eklenen++
        }
        elseif ($Code.Contains("D")) {
            $Label = "SILINDI"; $Color = "Red"; $Silinen++
        }
        elseif ($Code.Contains("R")) {
            $Label = "YENI AD"; $Color = "Cyan"; $Degisen++
        }
        elseif ($Code.Contains("M")) {
            $Label = "DEGISTI"; $Color = "Yellow"; $Degisen++
        }
        else {
            $Label = $Code.Trim(); $Color = "Gray"; $Degisen++
        }

        if ($Shown -lt $MaxShown) {
            Write-Host ("  {0,-9} {1}" -f $Label, $C.Path) -ForegroundColor $Color
            $Shown++
        }
    }

    if ($Changes.Count -gt $MaxShown) {
        Write-Host "  ... ve $($Changes.Count - $MaxShown) dosya daha" -ForegroundColor Gray
    }

    Write-Host ""
    Write-Host ("-" * 60) -ForegroundColor DarkGray
    Write-Warn "Toplam: $($Changes.Count) degisiklik"
    Write-Host ("  + Eklenen: {0}  |  Degisen: {1}  |  - Silinen: {2}" -f $Eklenen, $Degisen, $Silinen) -ForegroundColor White
    if ($Conflict -gt 0) { Write-Err "  Conflict: $Conflict dosya (13 numarali secenegi kullanin)" }
}

function Get-OperationInProgress {
    if (-not $script:GitDir) { return $null }

    if (Test-Path (Join-Path $script:GitDir "MERGE_HEAD")) { return "Merge" }
    if ((Test-Path (Join-Path $script:GitDir "rebase-merge")) -or
        (Test-Path (Join-Path $script:GitDir "rebase-apply"))) { return "Rebase" }
    if (Test-Path (Join-Path $script:GitDir "CHERRY_PICK_HEAD")) { return "Cherry-pick" }
    if (Test-Path (Join-Path $script:GitDir "REVERT_HEAD")) { return "Revert" }

    return $null
}

function Initialize-SyncContext {
    $Info = Get-BranchInfo

    if ($Info.Detached) {
        Write-Err "Detached HEAD durumundasiniz. Once bir branch'e gecin (15)."
        return $null
    }

    $Op = Get-OperationInProgress
    if ($Op) {
        Write-Err "Yarim kalmis bir $Op islemi var. Once 13 numarali secenekle cozun."
        return $null
    }

    Write-Warn "GitHub bilgileri aliniyor (fetch)..."
    git fetch --prune origin | Out-Host

    if ($LASTEXITCODE -ne 0) {
        Write-Err "GitHub'dan bilgi alinamadi (internet, yetki veya remote ayari kontrol edin)."
        return $null
    }

    return (Get-BranchInfo)
}

# ------------------------------------------------------------
# RISK / GUVENLIK KONTROLLERI
# ------------------------------------------------------------

function Get-RiskReason {
    param([string]$RelativePath)

    $N = "\" + $RelativePath.Replace("/", "\")

    foreach ($P in $Config.RiskyFolderPatterns) {
        if ($N -like "*$P*") { return "Klasor: $P" }
    }

    $Name = Split-Path $N -Leaf

    foreach ($P in $Config.RiskyFilePatterns) {
        if ($Name -like $P) { return "Dosya turu: $P" }
    }

    $Ext = [System.IO.Path]::GetExtension($Name).ToLowerInvariant()
    if (@($Config.CodeExtensions) -notcontains $Ext) {
        foreach ($P in $Config.NameHintPatterns) {
            if ($Name -like $P) { return "Adi hassas gorunuyor: $P" }
        }
    }

    return $null
}

function Find-RiskyChanges {
    param($Changes)

    $Found = @()
    foreach ($C in @($Changes)) {
        $Reason = Get-RiskReason $C.Path
        if ($Reason) { $Found += [pscustomobject]@{ Path = $C.Path; Reason = $Reason } }
    }
    return , $Found
}

function Find-SecretsInStaged {
    $Hits = @()
    $File = ""

    $Diff = git -c core.quotepath=false diff --cached -U0 --no-color 2>$null

    foreach ($Line in $Diff) {
        if ($Line -match '^\+\+\+ b/(.+)$') { $File = $Matches[1]; continue }

        if ($Line.StartsWith("+") -and -not $Line.StartsWith("+++")) {
            foreach ($Key in $SecretRegex.Keys) {
                if ($Line -match $SecretRegex[$Key]) {
                    $Hits += [pscustomobject]@{ File = $File; Type = $Key }
                    break
                }
            }
        }
    }

    return , @($Hits | Sort-Object File, Type -Unique)
}

# ------------------------------------------------------------
# DOTNET YARDIMCILARI
# ------------------------------------------------------------

function Get-DotNetFiles {
    param([string]$Filter)

    $Ext = [System.IO.Path]::GetExtension($Filter)

    return @(Get-ChildItem -Path $script:RepoRoot -Recurse -Filter $Filter -File -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Extension -eq $Ext -and
            $_.FullName -notmatch '\\(bin|obj|node_modules|\.git|\.vs|_GAMABEL_BACKUPS)\\' -and
            $_.FullName -notlike "$($script:BackupRoot)*"
        })
}

function Test-TestProject {
    param($File)
    return ($File.BaseName -cmatch 'Tests?$')
}

function Select-Item {
    param(
        $Items,
        [scriptblock]$Label,
        [string]$Prompt,
        [switch]$Always
    )

    $Items = @($Items)

    if ($Items.Count -eq 1 -and -not $Always) { return $Items[0] }

    for ($i = 0; $i -lt $Items.Count; $i++) {
        Write-Host ("[{0}] {1}" -f ($i + 1), (& $Label $Items[$i]))
    }

    $S = Read-Host $Prompt

    if ($S -match '^\d+$') {
        $Ix = [int]$S - 1
        if ($Ix -ge 0 -and $Ix -lt $Items.Count) { return $Items[$Ix] }
    }

    Write-Err "Gecersiz secim."
    return $null
}

function Get-RunProject {
    $All = @(Get-DotNetFiles "*.csproj" | Where-Object { -not (Test-TestProject $_) })

    if ($All.Count -eq 0) {
        Write-Err "Calistirilacak .csproj bulunamadi."
        return $null
    }

    $Runnable = @($All | Where-Object {
            $Text = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
            $Text -match 'Microsoft\.NET\.Sdk\.Web' -or $Text -match '<OutputType>\s*(Exe|WinExe)'
        })

    if ($Runnable.Count -gt 0) { $All = $Runnable }

    Write-Host ""
    return (Select-Item -Items $All -Label { param($f) $f.FullName } -Prompt "Projeyi secin")
}

function Get-BuildTarget {
    $Slns = @(Get-DotNetFiles "*.sln")

    if ($Slns.Count -gt 0) {
        return (Select-Item -Items $Slns -Label { param($f) $f.FullName } -Prompt "Cozumu secin")
    }

    $Proj = Get-RunProject
    return $Proj
}

function Invoke-ProjectBuild {
    param($Target)

    dotnet build $Target.FullName | Out-Host
    return ($LASTEXITCODE -eq 0)
}

# $true = basarili, $false = basarisiz, $null = test projesi yok
function Invoke-ProjectTests {
    $TestProjects = @(Get-DotNetFiles "*.csproj" | Where-Object { Test-TestProject $_ })

    if ($TestProjects.Count -eq 0) { return $null }

    $Sln = @(Get-DotNetFiles "*.sln")

    if ($Sln.Count -eq 1) {
        dotnet test $Sln[0].FullName | Out-Host
        return ($LASTEXITCODE -eq 0)
    }

    $Ok = $true
    foreach ($T in $TestProjects) {
        dotnet test $T.FullName | Out-Host
        if ($LASTEXITCODE -ne 0) { $Ok = $false }
    }
    return $Ok
}

# Commit oncesi build + test. $true donerse devam edilebilir.
function Invoke-PreCommitChecks {
    if (-not (Test-DotNet)) { return $true }

    if (-not (Confirm-Action "Commit/push oncesi build ve testler calistirilsin mi?" $true)) { return $true }

    $Target = Get-BuildTarget
    if ($null -ne $Target) {
        Write-Warn "BUILD CALISTIRILIYOR..."
        if (Invoke-ProjectBuild $Target) {
            Write-Ok "BUILD BASARILI."
        }
        else {
            Write-Err "BUILD BASARISIZ."
            if (-not (Confirm-Action "Yine de devam edilsin mi?" $false)) { return $false }
        }
    }

    $TestResult = Invoke-ProjectTests
    if ($null -eq $TestResult) {
        Write-Host "Test projesi bulunamadi, testler atlandi." -ForegroundColor Gray
    }
    elseif ($TestResult) {
        Write-Ok "TESTLER BASARILI."
    }
    else {
        Write-Err "TESTLER BASARISIZ."
        if (-not (Confirm-Action "Yine de devam edilsin mi?" $false)) { return $false }
    }

    return $true
}

# ------------------------------------------------------------
# COMMIT / PUSH / STASH YARDIMCILARI
# ------------------------------------------------------------

function Test-GitIdentity {
    $Name = git config user.name 2>$null
    $Mail = git config user.email 2>$null

    if ([string]::IsNullOrWhiteSpace($Name) -or [string]::IsNullOrWhiteSpace($Mail)) {
        Write-Err "Git kimligi tanimli degil. Su komutlari calistirin:"
        Write-Host '  git config --global user.name  "Adiniz"'
        Write-Host '  git config --global user.email "mail@ornek.com"'
        return $false
    }
    return $true
}

function Invoke-Commit {
    param([string]$Message)

    $Tmp = [System.IO.Path]::GetTempFileName()

    try {
        # Mesaj dosyadan okunur: PowerShell 5.1'de native arguman kodlama sorunu olmaz.
        [System.IO.File]::WriteAllText($Tmp, $Message, (New-Object System.Text.UTF8Encoding($false)))
        git -c i18n.commitEncoding=utf-8 commit -F $Tmp | Out-Host
        return ($LASTEXITCODE -eq 0)
    }
    finally {
        Remove-Item $Tmp -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-Push {
    param($Info)

    if ($Info.HasUpstream) {
        git push | Out-Host
    }
    else {
        Write-Warn "Upstream yok: branch GitHub'a ilk kez gonderiliyor (-u)."
        git push -u origin $Info.Branch | Out-Host
    }

    return ($LASTEXITCODE -eq 0)
}

function Undo-Stage {
    param([bool]$HadStagedBefore)

    if (-not $HadStagedBefore) {
        git reset -q | Out-Null
        Write-Host "Stage geri alindi (dosyalar oldugu gibi duruyor)." -ForegroundColor Gray
    }
}

# Kirli calisma alanini stash'e alip islemi yapar, sonra geri getirir.
function Invoke-WithStash {
    param([scriptblock]$Action)

    $Dirty = @(Get-LocalChanges).Count -gt 0
    $Stashed = $false

    if ($Dirty) {
        $Before = @(git stash list 2>$null).Count
        $Msg = "gamabel-auto-" + (Get-Date -Format 'yyyyMMdd-HHmmss')

        Write-Warn "Lokal degisiklikler gecici olarak stash'e aliniyor..."
        git stash push -u -m $Msg | Out-Host

        if ($LASTEXITCODE -ne 0) {
            Write-Err "Stash basarisiz. Islem iptal edildi, dosyalariniza dokunulmadi."
            return $false
        }

        $After = @(git stash list 2>$null).Count
        $Stashed = ($After -gt $Before)
    }

    $Ok = [bool](& $Action)

    if ($Stashed) {
        if (-not $Ok -and (Get-OperationInProgress)) {
            Write-Warn "Islem yarim kaldi. Degisiklikleriniz stash'te GUVENDE ('git stash list')."
            Write-Warn "Conflict'i cozdukten sonra 'git stash pop' ile geri alabilirsiniz."
            return $false
        }

        Write-Warn "Stash geri yukleniyor..."
        git stash pop | Out-Host

        if ($LASTEXITCODE -ne 0) {
            Write-Err "Stash geri yuklenirken conflict olustu. Degisiklikleriniz stash'te de duruyor."
            Write-Warn "13 numarali secenekle conflict dosyalarini kontrol edin."
            return $false
        }
    }

    return $Ok
}

function Invoke-FastForward {
    return (Invoke-WithStash {
            git merge --ff-only "@{u}" | Out-Host
            return ($LASTEXITCODE -eq 0)
        })
}

# ------------------------------------------------------------
# 1 - UYGULAMAYI CALISTIR
# ------------------------------------------------------------

function Start-Application {
    Write-Banner "UYGULAMAYI CALISTIR"

    if (-not (Test-DotNet)) { Wait-Menu; return }

    $Project = Get-RunProject
    if ($null -eq $Project) { Wait-Menu; return }

    Write-Ok "Proje: $($Project.FullName)"

    if (Confirm-Action "Ayri bir pencerede calistirilsin mi? (bu menu acik kalir)" $true) {
        Start-Process -FilePath "cmd.exe" -WorkingDirectory $Project.DirectoryName `
            -ArgumentList "/k", "dotnet run --project `"$($Project.FullName)`""
        Write-Ok "Uygulama yeni pencerede baslatildi."
    }
    else {
        Write-Warn "dotnet run baslatiliyor (durdurmak icin Ctrl+C)..."
        dotnet run --project $Project.FullName
        Write-Warn "Uygulama sonlandi."
    }

    Wait-Menu
}

# ------------------------------------------------------------
# 2 - BUILD
# ------------------------------------------------------------

function Invoke-ApplicationBuild {
    Write-Banner "PROJE BUILD"

    if (-not (Test-DotNet)) { Wait-Menu; return }

    $Target = Get-BuildTarget
    if ($null -eq $Target) { Wait-Menu; return }

    if (Invoke-ProjectBuild $Target) {
        Write-Ok "BUILD BASARILI."
    }
    else {
        Write-Err "BUILD BASARISIZ."
    }

    Wait-Menu
}

# ------------------------------------------------------------
# 3 - TEST
# ------------------------------------------------------------

function Test-Application {
    Write-Banner "TESTLER"

    if (-not (Test-DotNet)) { Wait-Menu; return }

    $Result = Invoke-ProjectTests

    if ($null -eq $Result) { Write-Warn "Test projesi bulunamadi." }
    elseif ($Result) { Write-Ok "TESTLER BASARILI." }
    else { Write-Err "TESTLER BASARISIZ." }

    Wait-Menu
}

# ------------------------------------------------------------
# 4 - GIT DURUMU
# ------------------------------------------------------------

function Show-GitStatus {
    Write-Banner "GIT DURUMU"

    git status
    Write-Host ""
    Write-Info "SON COMMIT"
    git --no-pager log -1 --oneline

    $Stashes = @(git stash list 2>$null)
    if ($Stashes.Count -gt 0) {
        Write-Host ""
        Write-Warn "Stash'te $($Stashes.Count) kayit var:"
        $Stashes | Select-Object -First 5 | ForEach-Object { Write-Host "  $_" }
    }

    Wait-Menu
}

# ------------------------------------------------------------
# 5 - GITHUB -> LOKAL
# ------------------------------------------------------------

function Update-FromRemote {
    Write-Banner "GITHUB GUNCELLEMELERINI LOKALE UYGULA"

    $Ctx = Initialize-SyncContext
    if ($null -eq $Ctx) { Wait-Menu; return }

    $Changes = @(Get-LocalChanges)

    if (-not $Ctx.HasUpstream) {
        Write-Warn "Bu branch icin upstream (GitHub karsiligi) tanimli degil."
        Write-Warn "6 numarali secenek branch'i GitHub'a ilk kez gonderebilir."
        Wait-Menu
        return
    }

    Write-Host ""
    Write-Warn ("GitHub'da yeni commit  : {0}" -f $Ctx.Behind)
    Write-Warn ("Lokal gonderilmemis    : {0}" -f $Ctx.Ahead)
    Write-Warn ("Lokal dosya degisikligi: {0}" -f $Changes.Count)

    if ($Ctx.Behind -eq 0) {
        Write-Host ""
        Write-Ok "GitHub'da uygulanacak yeni guncelleme yok."
        Wait-Menu
        return
    }

    if ($Ctx.Ahead -gt 0) {
        Write-Host ""
        Write-Err "Lokal ve GitHub AYRISMIS (her iki tarafta farkli commitler var)."
        Write-Warn "Fast-forward mumkun degil. 8 numarali Akilli Senkronizasyon'u kullanin."
        Wait-Menu
        return
    }

    Write-Host ""
    Write-Info "GitHub'daki yeni commitler:"
    git --no-pager log --oneline "HEAD..@{u}"
    Write-Host ""

    if (-not (Confirm-Action "Bu guncellemeler lokale uygulansin mi?" $true)) {
        Write-Warn "Islem iptal edildi."
        Wait-Menu
        return
    }

    if ($Changes.Count -gt 0) {
        Write-Host ""
        Write-Err "UYARI: Commit edilmemis lokal degisiklikler var."
        Show-LocalChanges
        Write-Host ""
        Write-Warn "Degisiklikler gecici stash'e alinip guncellemeden sonra geri yuklenecek."

        if (Confirm-Action "Guvenlik icin once proje yedegi alinsin mi?" $true) {
            if (-not (New-ProjectBackup)) {
                Write-Err "Yedek basarisiz. Islem iptal edildi."
                Wait-Menu
                return
            }
        }

        if (-not (Confirm-Action "Devam edilsin mi?" $true)) {
            Write-Warn "Islem iptal edildi."
            Wait-Menu
            return
        }
    }

    if (Invoke-FastForward) {
        Write-Ok "GITHUB GUNCELLEMELERI LOKALE BASARIYLA UYGULANDI."
    }
    else {
        Write-Err "GUNCELLEME TAMAMLANAMADI."
        Write-Warn "Mevcut kodunuz zorla ezilmedi."
    }

    Wait-Menu
}

# ------------------------------------------------------------
# 6 - LOKAL -> GITHUB
# ------------------------------------------------------------

function Send-ToRemote {
    param([datetime]$StartTime = (Get-Date))

    Write-Banner "LOKAL DEGISIKLIKLERI GITHUB'A YUKLE"

    $Ctx = Initialize-SyncContext
    if ($null -eq $Ctx) { Wait-Menu; return }

    $Changes = @(Get-LocalChanges)

    Write-Warn ("GitHub'dan geride     : {0}" -f $Ctx.Behind)
    Write-Warn ("Gonderilmemis commit  : {0}" -f $Ctx.Ahead)
    Write-Warn ("Commit edilmemis dosya: {0}" -f $Changes.Count)

    if ($Ctx.HasUpstream -and $Ctx.Behind -gt 0) {
        Write-Host ""
        Write-Err "UYARI: GitHub'da sizde olmayan yeni commitler var!"
        git --no-pager log --oneline "HEAD..@{u}"
        Write-Host ""
        Write-Warn "Once 5 (veya ayrismissa 8) numarali secenegi kullanin."
        Wait-Menu
        return
    }

    # --- Sadece daha once commit edilmis ama gonderilmemis isler ---
    if ($Changes.Count -eq 0) {
        if ($Ctx.HasUpstream -and $Ctx.Ahead -eq 0) {
            Write-Host ""
            Write-Ok "GitHub'a gonderilecek bir sey yok."
            Wait-Menu
            return
        }

        Write-Host ""
        if ($Ctx.HasUpstream) {
            Write-Info "GitHub'a gonderilmemis commitler:"
            git --no-pager log --oneline "@{u}..HEAD"
        }
        else {
            Write-Info "Branch GitHub'da henuz yok. Son commitler:"
            git --no-pager log --oneline -5
        }
        Write-Host ""

        if (-not (Confirm-Action "Bu commitler GitHub'a push edilsin mi?" $true)) {
            Write-Warn "Push yapilmadi."
            Wait-Menu
            return
        }

        if (-not (Invoke-PreCommitChecks)) { Write-Warn "Push iptal edildi."; Wait-Menu; return }

        if (Invoke-Push $Ctx) {
            Write-Ok "GITHUB'A BASARIYLA YUKLENDI."
            Complete-PushOperation -Message "Mevcut commitler push edildi" -FileCount 0 -Branch $Ctx.Branch -StartTime $StartTime
        }
        else {
            Write-Err "PUSH BASARISIZ. Commitler lokalde duruyor."
        }

        Wait-Menu
        return
    }

    # --- Commit edilmemis degisiklikler ---
    Write-Host ""
    Show-LocalChanges

    $Risky = @(Find-RiskyChanges $Changes)

    if ($Risky.Count -gt 0) {
        Write-Host ""
        Write-Err ("=" * 60)
        Write-Err "                    RISKLI DOSYA UYARISI"
        Write-Err ("=" * 60)
        foreach ($R in $Risky) { Write-Err ("  ! {0}   [{1}]" -f $R.Path, $R.Reason) }
        Write-Host ""
        Write-Warn "Bu dosyalar log, yedek, ayar veya gizli bilgi icerebilir."
        Write-Warn "Cogu zaman .gitignore ile disarida tutulmalidir (16 numarali secenek)."
        Write-Host ""

        if (-not (Confirm-Action "Yine de devam edilsin mi?" $false)) {
            Write-Warn "Push islemi iptal edildi."
            Wait-Menu
            return
        }
    }

    if (-not (Test-GitIdentity)) { Wait-Menu; return }

    $HadStaged = (@($Changes | Where-Object { $_.Staged }).Count -gt 0)

    Write-Host ""
    Write-Warn "Degisiklikler stage'e aliniyor (git add -A)..."
    git add -A

    if ($LASTEXITCODE -ne 0) {
        Write-Err "git add basarisiz."
        Wait-Menu
        return
    }

    Write-Host ""
    Write-Info "STAGE EDILEN DEGISIKLIKLER"
    git --no-pager diff --cached --stat

    # Icerik bazli secret taramasi
    $Secrets = @(Find-SecretsInStaged)

    if ($Secrets.Count -gt 0) {
        Write-Host ""
        Write-Err ("=" * 60)
        Write-Err "        OLASI GIZLI BILGI (SECRET) TESPIT EDILDI"
        Write-Err ("=" * 60)
        foreach ($S in $Secrets) { Write-Err ("  ! {0}   [{1}]" -f $S.File, $S.Type) }
        Write-Host ""
        Write-Warn "Parolalari/anahtarlari koda gommeyin: User Secrets veya ortam degiskeni kullanin."
        Write-Warn "Yanlis alarm olabilir (ornek: parola alani tanimi). Dosyayi kontrol edin."
        Write-Host ""

        if (-not (Confirm-Action "Yine de commit'e devam edilsin mi?" $false)) {
            Undo-Stage $HadStaged
            Write-Warn "Islem iptal edildi."
            Wait-Menu
            return
        }
    }

    if (Confirm-Action "Tam diff gosterilsin mi?" $false) {
        Write-Host ""
        git --no-pager diff --cached
        Write-Host ""
    }

    if (-not (Invoke-PreCommitChecks)) {
        Undo-Stage $HadStaged
        Write-Warn "Islem iptal edildi."
        Wait-Menu
        return
    }

    # Commit mesaji (bos birakilamaz, 'iptal' yazilirsa cikilir)
    do {
        Write-Host ""
        $Mesaj = Read-Host "Commit mesaji (iptal icin 'iptal' yazin)"

        if ($Mesaj -ieq "iptal") {
            Undo-Stage $HadStaged
            Write-Warn "Islem iptal edildi."
            Wait-Menu
            return
        }

        if ([string]::IsNullOrWhiteSpace($Mesaj)) { Write-Err "Commit mesaji bos olamaz." }
    }
    while ([string]::IsNullOrWhiteSpace($Mesaj))

    $Mesaj = $Mesaj.Trim()
    if ($Config.AsciiCommitMessages) { $Mesaj = ConvertTo-LatinChars $Mesaj }

    if (-not (Invoke-Commit $Mesaj)) {
        Write-Err "Commit basarisiz."
        Wait-Menu
        return
    }

    Write-Ok "COMMIT BASARILI."
    Write-Host ""
    git --no-pager show --stat --oneline HEAD

    # Kullanici beklerken GitHub'a yeni commit gelmis olabilir: tekrar kontrol.
    Write-Host ""
    Write-Warn "Push oncesi GitHub tekrar kontrol ediliyor..."
    git fetch --prune origin | Out-Host

    if ($LASTEXITCODE -ne 0) {
        Write-Err "GitHub kontrolu basarisiz. Push yapilmadi (commit lokalde kayitli)."
        Wait-Menu
        return
    }

    $After = Get-BranchInfo

    if ($After.HasUpstream -and $After.Behind -gt 0) {
        Write-Host ""
        Write-Err "DURUM DEGISTI: GitHub'a yeni commit gelmis! Guvenlik nedeniyle push yapilmadi."
        Write-Warn "Commit lokalde kayitli. 8 numarali Akilli Senkronizasyon'u kullanin."
        Wait-Menu
        return
    }

    if (-not (Confirm-Action "Commit GitHub'a push edilsin mi?" $true)) {
        Write-Warn "Push yapilmadi. Commit lokalde kayitli."
        Wait-Menu
        return
    }

    if (Invoke-Push $After) {
        Write-Ok "DEGISIKLIKLER GITHUB'A BASARIYLA YUKLENDI."
        Complete-PushOperation -Message $Mesaj -FileCount $Changes.Count -Branch $After.Branch -StartTime $StartTime
    }
    else {
        Write-Err "PUSH BASARISIZ. Commit lokalde kayitli, daha sonra tekrar deneyebilirsiniz."
    }

    Wait-Menu
}

# ------------------------------------------------------------
# 7 - GITHUB DURUMU
# ------------------------------------------------------------

function Show-RemoteStatus {
    Write-Banner "GITHUB DURUMU"

    $Ctx = Initialize-SyncContext
    if ($null -eq $Ctx) { Wait-Menu; return }

    $Changes = @(Get-LocalChanges)

    Write-Host ""
    Write-Host ("Branch                 : {0}" -f $Ctx.Branch)

    if (-not $Ctx.HasUpstream) {
        Write-Warn "Upstream               : yok (branch GitHub'a gonderilmemis)"
        Write-Warn ("Commit edilmemis dosya : {0}" -f $Changes.Count)
        Write-Host ""
        Write-Warn "6 numarali secenek branch'i GitHub'a ilk kez gonderebilir."
        Wait-Menu
        return
    }

    Write-Host ("Upstream               : {0}" -f $Ctx.Upstream)
    Write-Warn ("GitHub'dan geride      : {0} commit" -f $Ctx.Behind)
    Write-Warn ("GitHub'a gonderilmemis : {0} commit" -f $Ctx.Ahead)
    Write-Warn ("Commit edilmemis dosya : {0}" -f $Changes.Count)
    Write-Host ""

    if ($Ctx.Behind -eq 0 -and $Ctx.Ahead -eq 0 -and $Changes.Count -eq 0) {
        Write-Ok "LOKAL VE GITHUB TAM SENKRON."
    }
    elseif ($Ctx.Behind -gt 0 -and $Ctx.Ahead -gt 0) {
        Write-Err "UYARI: LOKAL VE GITHUB AYRISMIS. 8 numarali secenegi kullanin."
    }
    elseif ($Ctx.Behind -gt 0) {
        Write-Warn "GITHUB'DA YENI GUNCELLEMELER VAR (5 veya 8)."
    }
    elseif ($Ctx.Ahead -gt 0) {
        Write-Warn "GONDERILMEMIS COMMITLER VAR (6 veya 8)."
    }
    else {
        Write-Warn "COMMIT EDILMEMIS DEGISIKLIKLER VAR (6)."
    }

    if ($Changes.Count -gt 0) { Write-Host ""; Show-LocalChanges }

    if ($Ctx.Behind -gt 0) {
        Write-Host ""
        Write-Info "GITHUB'DAKI YENI COMMITLER:"
        git --no-pager log --oneline "HEAD..@{u}"
    }

    if ($Ctx.Ahead -gt 0) {
        Write-Host ""
        Write-Info "LOKALDEKI GONDERILMEMIS COMMITLER:"
        git --no-pager log --oneline "@{u}..HEAD"
    }

    Wait-Menu
}

# ------------------------------------------------------------
# 8 - AKILLI SENKRONIZASYON
# ------------------------------------------------------------

function Invoke-SmartSync {
    Write-Banner "AKILLI SENKRONIZASYON"

    $Ctx = Initialize-SyncContext
    if ($null -eq $Ctx) { Wait-Menu; return }

    $Changes = @(Get-LocalChanges)
    $Dirty = $Changes.Count -gt 0

    if (-not $Ctx.HasUpstream) {
        Write-Warn "Bu branch icin upstream yok. 6 numarali secenekle GitHub'a gonderin."
        Wait-Menu
        return
    }

    Write-Host ""
    Write-Warn ("GitHub'dan geride : {0}" -f $Ctx.Behind)
    Write-Warn ("Lokal ileri       : {0}" -f $Ctx.Ahead)
    Write-Warn ("Lokal degisiklik  : {0}" -f $Changes.Count)
    Write-Host ""

    # 1) Tam senkron
    if ($Ctx.Behind -eq 0 -and $Ctx.Ahead -eq 0 -and -not $Dirty) {
        Write-Ok "SISTEM TAM SENKRON."
        Wait-Menu
        return
    }

    # 2) Sadece commit edilmemis degisiklik
    if ($Ctx.Behind -eq 0 -and $Ctx.Ahead -eq 0 -and $Dirty) {
        Write-Warn "Sadece commit edilmemis lokal degisiklikler var."
        Show-LocalChanges
        Write-Host ""
        Write-Ok "6 numarali secenekle commit + push yapabilirsiniz."
        Wait-Menu
        return
    }

    # 3) Sadece GitHub ilerde -> fast-forward (kirliyse stash ile)
    if ($Ctx.Behind -gt 0 -and $Ctx.Ahead -eq 0) {
        Write-Warn "GitHub'da yeni guncellemeler var:"
        git --no-pager log --oneline "HEAD..@{u}"
        Write-Host ""

        if ($Dirty) {
            Write-Warn "Lokal degisiklikler var: gecici stash'e alinip guncelleme sonrasi geri yuklenecek."
            if (Confirm-Action "Guvenlik yedegi alinsin mi?" $true) {
                if (-not (New-ProjectBackup)) { Write-Err "Yedek basarisiz, iptal."; Wait-Menu; return }
            }
        }

        if (Confirm-Action "GitHub guncellemeleri lokale uygulansin mi?" $true) {
            if (Invoke-FastForward) {
                Write-Ok "Senkronizasyon tamamlandi."
            }
            else { Write-Err "Guncelleme tamamlanamadi." }
        }

        Wait-Menu
        return
    }

    # 4) Sadece lokal commit ileride -> push (kirli dosyalara dokunmaz)
    if ($Ctx.Behind -eq 0 -and $Ctx.Ahead -gt 0) {
        Write-Warn "Lokal commitler GitHub'a gonderilmemis:"
        git --no-pager log --oneline "@{u}..HEAD"
        if ($Dirty) { Write-Host ""; Write-Host "(Commit edilmemis $($Changes.Count) dosya lokalde kalacak.)" -ForegroundColor Gray }
        Write-Host ""

        if (Confirm-Action "Lokal commitler GitHub'a push edilsin mi?" $true) {
            if (Invoke-Push $Ctx) {
                Write-Ok "Senkronizasyon tamamlandi."
            }
            else { Write-Err "Push basarisiz." }
        }

        Wait-Menu
        return
    }

    # 5) Ayrismis durum
    Write-Err "KRITIK DURUM: Lokal ve GitHub ayrismis."
    Write-Host ""
    Write-Info "GitHub'daki commitler:"
    git --no-pager log --oneline "HEAD..@{u}"
    Write-Host ""
    Write-Info "Lokal commitler:"
    git --no-pager log --oneline "@{u}..HEAD"
    Write-Host ""
    Write-Host "  [1] Merge   (iki tarihi birlestirir, merge commit olusturur - en guvenlisi)"
    Write-Host "  [2] Rebase  (lokal commitlerinizi GitHub'in ustune tasir - temiz gecmis)"
    Write-Host "  [0] Iptal   (hicbir sey yapma)"
    Write-Host ""

    $Secim = Read-Host "Seciminiz"

    if ($Secim -ne "1" -and $Secim -ne "2") {
        Write-Warn "Islem iptal edildi. Hicbir sey degismedi."
        Wait-Menu
        return
    }

    if ($Dirty) {
        Write-Warn "Lokal degisiklikler stash ile korunacak."
        if (Confirm-Action "Guvenlik yedegi alinsin mi?" $true) {
            if (-not (New-ProjectBackup)) { Write-Err "Yedek basarisiz, iptal."; Wait-Menu; return }
        }
    }

    if (-not (Confirm-Action "Devam edilsin mi?" $true)) {
        Write-Warn "Islem iptal edildi."
        Wait-Menu
        return
    }

    if ($Secim -eq "1") {
        $Ok = Invoke-WithStash {
            git merge --no-edit "@{u}" | Out-Host
            return ($LASTEXITCODE -eq 0)
        }
    }
    else {
        $Ok = Invoke-WithStash {
            git rebase "@{u}" | Out-Host
            return ($LASTEXITCODE -eq 0)
        }
    }

    if (-not $Ok) {
        Write-Err "Birlestirme tamamlanamadi (conflict olabilir)."
        Write-Warn "13 numarali secenekle conflict dosyalarini gorup islemi cozebilir veya iptal edebilirsiniz."
        Wait-Menu
        return
    }

    Write-Ok "Birlestirme basarili."

    if (Confirm-Action "Simdi build/test calistirip GitHub'a push edelim mi?" $true) {
        if (Invoke-PreCommitChecks) {
            $Fresh = Get-BranchInfo
            if (Invoke-Push $Fresh) { Write-Ok "GitHub'a yuklendi." }
            else { Write-Err "Push basarisiz." }
        }
    }

    Wait-Menu
}

# ------------------------------------------------------------
# 9 - DETAYLI DEGISIKLIKLER
# ------------------------------------------------------------

function Show-DetailedChanges {
    Write-Banner "DEGISIKLIKLERI INCELE"

    $Changes = @(Get-LocalChanges)

    if ($Changes.Count -eq 0) {
        Write-Ok "Degisiklik yok."
        Wait-Menu
        return
    }

    Write-Info "Ozet (staged + unstaged):"
    git --no-pager diff HEAD --stat

    $Untracked = @(git -c core.quotepath=false ls-files --others --exclude-standard 2>$null)
    if ($Untracked.Count -gt 0) {
        Write-Host ""
        Write-Info "Yeni (takip edilmeyen) dosyalar: $($Untracked.Count)"
        $Untracked | Select-Object -First 30 | ForEach-Object { Write-Host "  + $_" }
        if ($Untracked.Count -gt 30) { Write-Host "  ... ve $($Untracked.Count - 30) dosya daha" -ForegroundColor Gray }
    }

    Write-Host ""
    if (Confirm-Action "Takip edilen dosyalarin tam farkini gormek ister misiniz?" $false) {
        git --no-pager diff HEAD
    }

    Wait-Menu
}

# ------------------------------------------------------------
# 10 - YEDEK
# ------------------------------------------------------------

function Remove-OldBackups {
    param([string]$Protect = "")

    try {
        $Old = @(Get-ChildItem -Path $script:BackupRoot -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            Select-Object -Skip ([int]$Config.BackupKeep))

        foreach ($D in $Old) {
            if ($Protect -and $D.FullName -eq $Protect) { continue }
            Remove-Item -Path $D.FullName -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "Eski yedek silindi: $($D.Name)" -ForegroundColor Gray
        }
    }
    catch {}
}

function New-ProjectBackup {
    param(
        [string]$Suffix = "",
        [string]$Protect = ""
    )

    try {
        if (-not (Get-Command robocopy -ErrorAction SilentlyContinue)) { throw "robocopy bulunamadi" }

        New-Item -ItemType Directory -Path $script:BackupRoot -Force | Out-Null

        $Stamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
        $Name = if ($Suffix) { "$Stamp-$Suffix" } else { $Stamp }
        $Dest = Join-Path $script:BackupRoot $Name

        New-Item -ItemType Directory -Path $Dest -Force | Out-Null

        Write-Warn "Proje yedegi aliniyor (bin, obj, .vs, node_modules haric)..."

        $Xd = @(".git", "bin", "obj", ".vs", "node_modules", $script:BackupRoot)
        $RoboArgs = @($script:RepoRoot, $Dest, "/E", "/XD") + $Xd + @("/NFL", "/NDL", "/NJH", "/NJS", "/NP", "/R:1", "/W:1")

        & robocopy @RoboArgs | Out-Null

        if ($LASTEXITCODE -ge 8) { throw "robocopy hata kodu: $LASTEXITCODE" }

        Write-Ok "YEDEK ALINDI: $Dest"

        Remove-OldBackups -Protect $Protect
        return $true
    }
    catch {
        Write-Err "YEDEK ALINAMADI: $($_.Exception.Message)"
        return $false
    }
}

function Backup-Menu {
    Write-Banner "PROJE YEDEGI"
    Write-Host "Yedek konumu: $script:BackupRoot" -ForegroundColor Gray
    Write-Host "Saklanan yedek sayisi: $($Config.BackupKeep)" -ForegroundColor Gray
    Write-Host ""
    New-ProjectBackup | Out-Null
    Wait-Menu
}

# ------------------------------------------------------------
# 18 - VERITABANI YEDEGI
# ------------------------------------------------------------

function ConvertTo-MySqlIdentifier {
    param([Parameter(Mandatory = $true)][string]$Value)
    return '`' + $Value.Replace('`', '``') + '`'
}

function ConvertTo-MySqlLiteral {
    param([Parameter(Mandatory = $true)][AllowNull()][object]$Value)

    if ($null -eq $Value -or $Value -is [DBNull]) { return "NULL" }
    if ($Value -is [byte[]]) { return "0x" + [BitConverter]::ToString($Value).Replace("-", "") }
    if ($Value -is [bool]) { return $(if ($Value) { "1" } else { "0" }) }
    if ($Value -is [datetime]) {
        return "'" + $Value.ToString("yyyy-MM-dd HH:mm:ss.ffffff", [Globalization.CultureInfo]::InvariantCulture) + "'"
    }
    if ($Value -is [timespan]) {
        return "'" + $Value.ToString("c", [Globalization.CultureInfo]::InvariantCulture) + "'"
    }
    if ($Value -is [string] -or $Value -is [char]) {
        $Text = [string]$Value
        $Text = $Text.Replace("\", "\\").Replace("'", "\'").Replace([string][char]0, "\0")
        $Text = $Text.Replace("`n", "\n").Replace("`r", "\r").Replace([string][char]26, "\Z")
        return "'" + $Text + "'"
    }
    if ($Value -is [IFormattable]) {
        return $Value.ToString($null, [Globalization.CultureInfo]::InvariantCulture)
    }

    $Text = [string]$Value
    return "'" + $Text.Replace("\", "\\").Replace("'", "\'") + "'"
}

function Get-DatabaseBackupConnectionString {
    $ConnectionString = [Environment]::GetEnvironmentVariable("ConnectionStrings__MyConnection")
    if (-not [string]::IsNullOrWhiteSpace($ConnectionString)) { return $ConnectionString }

    $Settings = @{}
    $EnvironmentName = [Environment]::GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    if ([string]::IsNullOrWhiteSpace($EnvironmentName)) {
        $EnvironmentName = [Environment]::GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    }

    $SettingsFiles = @((Join-Path $script:RepoRoot "appsettings.json"))
    if (-not [string]::IsNullOrWhiteSpace($EnvironmentName)) {
        $SettingsFiles += Join-Path $script:RepoRoot "appsettings.$EnvironmentName.json"
    }

    foreach ($SettingsFile in $SettingsFiles) {
        if (-not (Test-Path -LiteralPath $SettingsFile -PathType Leaf)) { continue }
        $Json = Get-Content -LiteralPath $SettingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($Json.ConnectionStrings -and $Json.ConnectionStrings.MyConnection) {
            $Settings["ConnectionString"] = [string]$Json.ConnectionStrings.MyConnection
        }
    }

    if ([string]::IsNullOrWhiteSpace($Settings["ConnectionString"])) {
        throw "Connection string bulunamadi. appsettings dosyalarini veya ConnectionStrings__MyConnection ortam degiskenini kontrol edin."
    }
    return $Settings["ConnectionString"]
}

function Get-MySqlCreateStatement {
    param(
        [Parameter(Mandatory = $true)][object]$Connection,
        [Parameter(Mandatory = $true)][string]$ObjectType,
        [Parameter(Mandatory = $true)][string]$ObjectName
    )

    $QuotedName = ConvertTo-MySqlIdentifier $ObjectName
    $CommandText = switch ($ObjectType) {
        "VIEW" { "SHOW CREATE VIEW $QuotedName;" }
        "TRIGGER" { "SHOW CREATE TRIGGER $QuotedName;" }
        "PROCEDURE" { "SHOW CREATE PROCEDURE $QuotedName;" }
        "FUNCTION" { "SHOW CREATE FUNCTION $QuotedName;" }
        "EVENT" { "SHOW CREATE EVENT $QuotedName;" }
        default { throw "Desteklenmeyen veritabani nesnesi: $ObjectType" }
    }

    $Command = $Connection.CreateCommand()
    $Command.CommandTimeout = 0
    $Command.CommandText = $CommandText
    $Reader = $Command.ExecuteReader()
    try {
        if (-not $Reader.Read()) { throw "$ObjectType nesnesinin DDL bilgisi okunamadi: $ObjectName" }
        for ($Index = 0; $Index -lt $Reader.FieldCount; $Index++) {
            $ColumnName = $Reader.GetName($Index)
            if ($ColumnName -like "Create *" -or $ColumnName -eq "SQL Original Statement") {
                return [string]$Reader.GetValue($Index)
            }
        }
        throw "$ObjectType nesnesinin CREATE ifadesi sunucudan alinamadi: $ObjectName"
    }
    finally {
        $Reader.Dispose()
        $Command.Dispose()
    }
}

function Get-DatabaseObjectName {
    param(
        [Parameter(Mandatory = $true)][object]$Connection,
        [Parameter(Mandatory = $true)][string]$ObjectType
    )

    $Command = $Connection.CreateCommand()
    $Command.CommandTimeout = 0
    $Command.CommandText = ""
    switch ($ObjectType) {
        "VIEW" {
            $Command.CommandText = "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'VIEW' ORDER BY TABLE_NAME;"
        }
        "TRIGGER" {
            $Command.CommandText = "SELECT TRIGGER_NAME FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA = DATABASE() ORDER BY TRIGGER_NAME;"
        }
        "PROCEDURE" {
            $Command.CommandText = "SELECT ROUTINE_NAME FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA = DATABASE() AND ROUTINE_TYPE = 'PROCEDURE' ORDER BY ROUTINE_NAME;"
        }
        "FUNCTION" {
            $Command.CommandText = "SELECT ROUTINE_NAME FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA = DATABASE() AND ROUTINE_TYPE = 'FUNCTION' ORDER BY ROUTINE_NAME;"
        }
        "EVENT" {
            $Command.CommandText = "SELECT EVENT_NAME FROM information_schema.EVENTS WHERE EVENT_SCHEMA = DATABASE() ORDER BY EVENT_NAME;"
        }
        default { throw "Desteklenmeyen veritabani nesnesi: $ObjectType" }
    }

    $Names = [System.Collections.Generic.List[string]]::new()
    $Reader = $Command.ExecuteReader()
    try {
        while ($Reader.Read()) { $Names.Add([string]$Reader.GetValue(0)) }
    }
    finally {
        $Reader.Dispose()
        $Command.Dispose()
    }
    return $Names.ToArray()
}

function Write-DatabaseObjectDefinition {
    param(
        [Parameter(Mandatory = $true)][object]$Connection,
        [Parameter(Mandatory = $true)][System.IO.StreamWriter]$Writer,
        [Parameter(Mandatory = $true)][string]$ObjectType
    )

    $Names = @(Get-DatabaseObjectNames -Connection $Connection -ObjectType $ObjectType)
    foreach ($Name in $Names) {
        $QuotedName = ConvertTo-MySqlIdentifier $Name
        $CreateStatement = Get-MySqlCreateStatement -Connection $Connection -ObjectType $ObjectType -ObjectName $Name
        $Writer.WriteLine("")
        $Writer.WriteLine("-- $ObjectType $Name")
        if ($ObjectType -eq "VIEW") {
            $Writer.WriteLine("DROP VIEW IF EXISTS $QuotedName;")
            $Writer.WriteLine("$CreateStatement;")
        }
        else {
            $DropType = $ObjectType.ToLowerInvariant()
            $Writer.WriteLine("DROP $DropType IF EXISTS $QuotedName;")
            $Writer.WriteLine("DELIMITER ;;")
            $Writer.WriteLine("$CreateStatement;;")
            $Writer.WriteLine("DELIMITER ;")
        }
    }
}

function Import-DatabaseBackupDependency {
    param(
        [Parameter(Mandatory = $true)][string]$PackageId,
        [Parameter(Mandatory = $true)][string]$Version,
        [Parameter(Mandatory = $true)][string]$NuGetRoot
    )

    if (-not $script:DatabaseBackupLoadedPackages) {
        $script:DatabaseBackupLoadedPackages = @{}
    }
    $PackageKey = "$PackageId/$Version".ToLowerInvariant()
    if ($script:DatabaseBackupLoadedPackages.ContainsKey($PackageKey)) { return }
    $script:DatabaseBackupLoadedPackages[$PackageKey] = $true

    $PackageDirectory = Join-Path $NuGetRoot "$($PackageId.ToLowerInvariant())\$Version"
    $NuspecPath = Join-Path $PackageDirectory "$($PackageId.ToLowerInvariant()).nuspec"
    if (-not (Test-Path -LiteralPath $NuspecPath -PathType Leaf)) {
        throw "NuGet bagimliligi bulunamadi: $PackageId $Version. Bagimliliklari indirmek icin dotnet restore calistirin."
    }

    [xml]$Nuspec = Get-Content -LiteralPath $NuspecPath -Raw -Encoding UTF8
    $DependencyNodes = @()
    $Groups = @($Nuspec.SelectNodes("//*[local-name()='dependencies']/*[local-name()='group']"))
    if ($Groups.Count -gt 0) {
        $FrameworkGroup = $Groups | Where-Object {
            $_.GetAttribute("targetFramework") -match '(?i)(net462|\.NETFramework4\.6\.2)'
        } | Select-Object -First 1
        if (-not $FrameworkGroup) {
            $FrameworkGroup = $Groups | Where-Object {
                $_.GetAttribute("targetFramework") -match '(?i)(net461|\.NETFramework4\.6\.1)'
            } | Select-Object -First 1
        }
        if (-not $FrameworkGroup) {
            $FrameworkGroup = $Groups | Where-Object {
                $_.GetAttribute("targetFramework") -match '(?i)(netstandard2\.0|\.NETStandard2\.0)'
            } | Select-Object -First 1
        }
        if ($FrameworkGroup) {
            $DependencyNodes = @($FrameworkGroup.SelectNodes("./*[local-name()='dependency']"))
        }
    }
    else {
        $DependencyNodes = @($Nuspec.SelectNodes("//*[local-name()='dependencies']/*[local-name()='dependency']"))
    }

    foreach ($DependencyNode in $DependencyNodes) {
        $DependencyPackageId = $DependencyNode.GetAttribute("id")
        $DependencyVersion = ($DependencyNode.GetAttribute("version") -replace '^[\[\(]', '').Split(',')[0].Trim().TrimEnd(']', ')')
        if ($DependencyPackageId -and $DependencyVersion) {
            Import-DatabaseBackupDependency -PackageId $DependencyPackageId -Version $DependencyVersion -NuGetRoot $NuGetRoot
        }
    }

    $LibraryRoot = Join-Path $PackageDirectory "lib"
    if (-not (Test-Path -LiteralPath $LibraryRoot -PathType Container)) { return }
    $AssemblyName = "$PackageId.dll"
    $AssemblyPath = Get-ChildItem -LiteralPath $LibraryRoot -Recurse -File -Filter $AssemblyName |
        Where-Object { $_.FullName -match '(?i)\\(net462|net461|netstandard2\.0)\\' } |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $AssemblyPath) {
        $AssemblyPath = Get-ChildItem -LiteralPath $LibraryRoot -Recurse -File -Filter $AssemblyName |
            Select-Object -First 1 -ExpandProperty FullName
    }
    if ($AssemblyPath) { [void][Reflection.Assembly]::LoadFrom($AssemblyPath) }
}

function Restore-MySqlConnectorFrameworkDependency {
    param(
        [Parameter(Mandatory = $true)][string]$Version
    )

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "MySqlConnector .NET Framework bagimliliklari eksik ve dotnet komutu bulunamadi."
    }

    $RestoreDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("gamabel-mysql-restore-" + [guid]::NewGuid().ToString("N"))
    try {
        New-Item -ItemType Directory -Path $RestoreDirectory -Force | Out-Null
        $ProjectPath = Join-Path $RestoreDirectory "DatabaseBackupDependencies.csproj"
        $ProjectContents = "<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><TargetFramework>net462</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=`"MySqlConnector`" Version=`"$Version`" /></ItemGroup></Project>"
        [System.IO.File]::WriteAllText($ProjectPath, $ProjectContents, [System.Text.Encoding]::UTF8)

        $null = & dotnet restore $ProjectPath --nologo --verbosity quiet 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "NuGet bagimliliklari geri yuklenemedi (dotnet restore cikis kodu: $LASTEXITCODE). Ag baglantisini ve NuGet kaynaklarini kontrol edin."
        }
    }
    finally {
        if (Test-Path -LiteralPath $RestoreDirectory) {
            try { Remove-Item -LiteralPath $RestoreDirectory -Recurse -Force -ErrorAction Stop }
            catch { Write-Warn "Gecici NuGet restore klasoru silinemedi: $RestoreDirectory ($($_.Exception.Message))" }
        }
    }
}

function Remove-OldDatabaseBackup {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = "Low")]
    param(
        [Parameter(Mandatory = $true)][string]$BackupDirectory,
        [Parameter(Mandatory = $true)][int]$KeepCount
    )

    $OldFiles = @(Get-ChildItem -LiteralPath $BackupDirectory -File -Filter "database_*.sql" |
        Sort-Object Name -Descending | Select-Object -Skip $KeepCount)
    foreach ($File in $OldFiles) {
        if ($PSCmdlet.ShouldProcess($File.FullName, "Delete old database backup")) {
            Remove-Item -LiteralPath $File.FullName -Force -ErrorAction Stop
            Write-Info "Eski veritabani yedegi silindi: $($File.Name)"
        }
    }
}

function Backup-Database {
    Write-Banner "TUM VERITABANI YEDEGI"

    $BackupDirectory = Join-Path $script:BackupRoot "Database"
    $TemporaryFile = $null
    $Connection = $null
    $Writer = $null

    try {
        $DatabaseBackupKeep = 15
        if (-not [int]::TryParse([string]$Config.DatabaseBackupKeep, [ref]$DatabaseBackupKeep) -or $DatabaseBackupKeep -lt 1) {
            throw "DatabaseBackupKeep ayari pozitif bir tam sayi olmalidir."
        }

        $ProjectFile = Join-Path $script:RepoRoot "gamabelmvc.csproj"
        if (-not (Test-Path -LiteralPath $ProjectFile -PathType Leaf)) {
            throw "gamabelmvc.csproj bulunamadi."
        }
        [xml]$Project = Get-Content -LiteralPath $ProjectFile -Raw -Encoding UTF8
        $Package = $Project.Project.ItemGroup.PackageReference |
            Where-Object { $_.Include -eq "MySqlConnector" } | Select-Object -First 1
        if (-not $Package -or -not $Package.Version) { throw "MySqlConnector surumu proje dosyasinda bulunamadi." }

        $NuGetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE ".nuget\packages" }
        $ConnectorPath = Join-Path $NuGetRoot "mysqlconnector\$($Package.Version)\lib\net462\MySqlConnector.dll"
        if (-not (Test-Path -LiteralPath $ConnectorPath -PathType Leaf)) {
            throw "MySqlConnector .NET Framework bagimliligi NuGet onbelleginde bulunamadi. Once 'dotnet restore' calistirin."
        }

        try {
            Import-DatabaseBackupDependency -PackageId "MySqlConnector" -Version ([string]$Package.Version) -NuGetRoot $NuGetRoot
        }
        catch {
            Write-Warn "MySqlConnector .NET Framework bagimliliklari eksik; NuGet uzerinden geri yukleniyor..."
            Restore-MySqlConnectorFrameworkDependency -Version ([string]$Package.Version)
            $script:DatabaseBackupLoadedPackages = @{}
            Import-DatabaseBackupDependency -PackageId "MySqlConnector" -Version ([string]$Package.Version) -NuGetRoot $NuGetRoot
        }

        [void][Reflection.Assembly]::LoadFrom($ConnectorPath)

        $ConnectionString = Get-DatabaseBackupConnectionString
        $Builder = [MySqlConnector.MySqlConnectionStringBuilder]::new($ConnectionString)
        if ([string]::IsNullOrWhiteSpace($Builder.Database)) {
            throw "Baglanti dizesinde hedef veritabani belirtilmemis."
        }
        $DatabaseName = [string]$Builder.Database
        $Connection = [MySqlConnector.MySqlConnection]::new($ConnectionString)
        $Connection.Open()

        New-Item -ItemType Directory -Path $BackupDirectory -Force | Out-Null
        $Stamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss-fff"
        $BackupFile = Join-Path $BackupDirectory "database_$Stamp.sql"
        $TemporaryFile = "$BackupFile.$([guid]::NewGuid().ToString('N')).tmp"
        $Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        $Writer = New-Object -TypeName System.IO.StreamWriter -ArgumentList $TemporaryFile, $false, $Utf8NoBom

        $Writer.WriteLine("-- GAMABEL MVC full database backup")
        $Writer.WriteLine("-- Database: $(ConvertTo-MySqlIdentifier $DatabaseName)")
        $Writer.WriteLine("-- Created: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
        $Writer.WriteLine("SET NAMES utf8mb4;")
        $Writer.WriteLine("SET SESSION sql_mode = TRIM(',' FROM REPLACE(CONCAT(',', @@SESSION.sql_mode, ','), ',NO_BACKSLASH_ESCAPES,', ','));")
        $Writer.WriteLine("SET FOREIGN_KEY_CHECKS = 0;")
        $Writer.WriteLine("USE $(ConvertTo-MySqlIdentifier $DatabaseName);")
        $Writer.WriteLine("")

        $Command = $Connection.CreateCommand()
        $Command.CommandTimeout = 0
        $Command.CommandText = "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME;"
        $Reader = $Command.ExecuteReader()
        $TableNames = [System.Collections.Generic.List[string]]::new()
        try {
            while ($Reader.Read()) { $TableNames.Add([string]$Reader.GetValue(0)) }
        }
        finally {
            $Reader.Dispose()
            $Command.Dispose()
        }
        if ($TableNames.Count -eq 0) { throw "Hedef veritabaninda yedeklenecek tablo bulunamadi." }

        foreach ($TableName in $TableNames) {
            $QuotedTable = ConvertTo-MySqlIdentifier $TableName
            $Writer.WriteLine("-- ----------------------------")
            $Writer.WriteLine("-- Table structure for $TableName")
            $Writer.WriteLine("-- ----------------------------")
            $Writer.WriteLine("DROP TABLE IF EXISTS $QuotedTable;")

            $Command = $Connection.CreateCommand()
            $Command.CommandTimeout = 0
            $Command.CommandText = "SHOW CREATE TABLE $QuotedTable;"
            $Reader = $Command.ExecuteReader()
            try {
                if (-not $Reader.Read()) { throw "Tablo DDL bilgisi okunamadi: $TableName" }
                $CreateIndex = -1
                for ($Index = 0; $Index -lt $Reader.FieldCount; $Index++) {
                    if ($Reader.GetName($Index) -eq "Create Table") { $CreateIndex = $Index; break }
                }
                if ($CreateIndex -lt 0) { throw "Tablo CREATE ifadesi alinamadi: $TableName" }
                $Writer.WriteLine($Reader.GetString($CreateIndex) + ";")
                $Writer.WriteLine("")
            }
            finally {
                $Reader.Dispose()
                $Command.Dispose()
            }
        }

        $TransactionCommand = $Connection.CreateCommand()
        $TransactionCommand.CommandText = "SET TRANSACTION ISOLATION LEVEL REPEATABLE READ; START TRANSACTION WITH CONSISTENT SNAPSHOT;"
        [void]$TransactionCommand.ExecuteNonQuery()
        $TransactionCommand.Dispose()

        foreach ($TableName in $TableNames) {
            $QuotedTable = ConvertTo-MySqlIdentifier $TableName
            $Writer.WriteLine("-- ----------------------------")
            $Writer.WriteLine("-- Records of $TableName")
            $Writer.WriteLine("-- ----------------------------")
            $Command = $Connection.CreateCommand()
            $Command.CommandTimeout = 0
            $Command.CommandText = "SELECT * FROM $QuotedTable;"
            $Reader = $Command.ExecuteReader()
            $RowCount = [long]0
            try {
                while ($Reader.Read()) {
                    $Values = for ($Index = 0; $Index -lt $Reader.FieldCount; $Index++) {
                        ConvertTo-MySqlLiteral $Reader.GetValue($Index)
                    }
                    $Writer.WriteLine("INSERT INTO $QuotedTable VALUES ($($Values -join ', '));")
                    $RowCount++
                }
            }
            finally {
                $Reader.Dispose()
                $Command.Dispose()
            }
            $Writer.WriteLine("")
            Write-Info "Yedeklendi: $TableName ($RowCount kayit)"
        }

        $TransactionCommand = $Connection.CreateCommand()
        $TransactionCommand.CommandText = "COMMIT;"
        [void]$TransactionCommand.ExecuteNonQuery()
        $TransactionCommand.Dispose()

        foreach ($ObjectType in @("VIEW", "TRIGGER", "PROCEDURE", "FUNCTION", "EVENT")) {
            Write-DatabaseObjectDefinition -Connection $Connection -Writer $Writer -ObjectType $ObjectType
        }

        $Writer.WriteLine("")
        $Writer.WriteLine("SET FOREIGN_KEY_CHECKS = 1;")
        $Writer.Flush()
        $Writer.Dispose()
        $Writer = $null
        $Connection.Dispose()
        $Connection = $null

        $TemporarySize = (Get-Item -LiteralPath $TemporaryFile).Length
        if ($TemporarySize -le 0) { throw "Olusturulan SQL yedegi bos." }
        Move-Item -LiteralPath $TemporaryFile -Destination $BackupFile -ErrorAction Stop
        $TemporaryFile = $null
        $FileInfo = Get-Item -LiteralPath $BackupFile

        Write-Ok "VERITABANI YEDEGI ALINDI: $BackupFile"
        Write-Info ("Dosya boyutu: {0:N2} MB" -f ($FileInfo.Length / 1MB))
        Write-Info ("Tablo sayisi: {0}; saklama adedi: {1}" -f $TableNames.Count, $DatabaseBackupKeep)
        try {
            Remove-OldDatabaseBackup -BackupDirectory $BackupDirectory -KeepCount $DatabaseBackupKeep
        }
        catch {
            Write-Warn "Yedek alindi ancak eski yedek temizligi basarisiz: $($_.Exception.Message)"
        }
    }
    catch {
        Write-Err "VERITABANI YEDEGI ALINAMADI: $($_.Exception.Message)"
    }
    finally {
        if ($Writer) {
            try { $Writer.Dispose() }
            catch { Write-Warn "Yedek dosyasini kapatirken hata: $($_.Exception.Message)" }
        }
        if ($Connection) {
            try { $Connection.Dispose() }
            catch { Write-Warn "Veritabani baglantisini kapatirken hata: $($_.Exception.Message)" }
        }
        if ($TemporaryFile -and (Test-Path -LiteralPath $TemporaryFile)) {
            try { Remove-Item -LiteralPath $TemporaryFile -Force -ErrorAction Stop }
            catch { Write-Warn "Gecici yedek dosyasi silinemedi: $TemporaryFile ($($_.Exception.Message))" }
        }
    }

    Wait-Menu
}

# ------------------------------------------------------------
# 11 - YEDEKTEN GERI YUKLE
# ------------------------------------------------------------

function Restore-ProjectBackup {
    Write-Banner "YEDEKTEN GERI YUKLE"

    if (-not (Test-Path $script:BackupRoot)) {
        Write-Warn "Yedek klasoru bulunamadi."
        Wait-Menu
        return
    }

    $Backups = @(Get-ChildItem -Path $script:BackupRoot -Directory | Sort-Object Name -Descending | Select-Object -First 15)

    if ($Backups.Count -eq 0) {
        Write-Warn "Yedek bulunamadi."
        Wait-Menu
        return
    }

    Write-Info "Mevcut yedekler (en yeni ustte):"
    $Selected = Select-Item -Items $Backups -Label { param($d) $d.Name } -Prompt "Geri yuklenecek yedegin numarasi (Enter = iptal)" -Always

    if ($null -eq $Selected) {
        Write-Warn "Geri yukleme iptal edildi."
        Wait-Menu
        return
    }

    Write-Host ""
    Write-Info "Secilen yedek: $($Selected.FullName)"
    Write-Err "DIKKAT: Mevcut proje dosyalarinin uzerine yazilacak."

    $Mirror = Confirm-Action "Yedekte OLMAYAN dosyalar da silinsin mi? (tam ayna, riskli)" $false

    $Onay = Read-Host "Onaylamak icin GERI YUKLE yazin"
    if ($Onay -cne "GERI YUKLE") {
        Write-Warn "Geri yukleme iptal edildi."
        Wait-Menu
        return
    }

    Write-Warn "Once mevcut durumun guvenlik yedegi aliniyor..."
    if (-not (New-ProjectBackup -Suffix "restore-oncesi" -Protect $Selected.FullName)) {
        Write-Err "Guvenlik yedegi alinamadi. Geri yukleme iptal edildi."
        Wait-Menu
        return
    }

    try {
        $Mode = if ($Mirror) { "/MIR" } else { "/E" }
        $Xd = @(".git", "bin", "obj", ".vs", "node_modules", $script:BackupRoot)
        $RoboArgs = @($Selected.FullName, $script:RepoRoot, $Mode, "/XD") + $Xd + @("/NFL", "/NDL", "/NJH", "/NJS", "/NP", "/R:1", "/W:1")

        & robocopy @RoboArgs | Out-Null

        if ($LASTEXITCODE -ge 8) { throw "robocopy hata kodu: $LASTEXITCODE" }

        Write-Ok "YEDEK GERI YUKLENDI: $($Selected.Name)"
    }
    catch {
        Write-Err "Geri yukleme basarisiz: $($_.Exception.Message)"
    }

    Wait-Menu
}

# ------------------------------------------------------------
# 12 - VS CODE
# ------------------------------------------------------------

function Open-VSCode {
    Write-Info ">> VS CODE ACILIYOR..."

    if (Get-Command code -ErrorAction SilentlyContinue) {
        code $script:RepoRoot
        Write-Ok "VS Code acildi."
    }
    else {
        Write-Err "'code' komutu bulunamadi. VS Code PATH ayarini kontrol edin."
    }

    Wait-Menu
}

# ------------------------------------------------------------
# 13 - CONFLICT / YARIM ISLEM
# ------------------------------------------------------------

function Show-ConflictCheck {
    Write-Banner "CONFLICT VE YARIM ISLEM KONTROLU"

    $Files = @(git -c core.quotepath=false diff --name-only --diff-filter=U 2>$null)
    $Op = Get-OperationInProgress

    if ($Files.Count -eq 0 -and -not $Op) {
        Write-Ok "Conflict veya yarim kalmis islem yok."
        Wait-Menu
        return
    }

    if ($Files.Count -gt 0) {
        Write-Err "CONFLICT BULUNAN DOSYALAR:"
        foreach ($F in $Files) { Write-Err "  $F" }
        Write-Host ""
        Write-Warn "Dosyalardaki <<<<<<< ======= >>>>>>> bolumlerini duzenleyin, sonra 'git add <dosya>' yapin."

        if (Get-Command code -ErrorAction SilentlyContinue) {
            if (Confirm-Action "Conflict dosyalari VS Code'da acilsin mi?" $true) {
                foreach ($F in $Files) { code $F }
            }
        }
    }

    if ($Op) {
        Write-Host ""
        Write-Warn "Yarim kalmis islem: $Op"
        Write-Host "Cozup tamamlamak icin: git add ... ve ardindan " -NoNewline
        switch ($Op) {
            "Merge"       { Write-Host "'git commit'" }
            "Rebase"      { Write-Host "'git rebase --continue'" }
            "Cherry-pick" { Write-Host "'git cherry-pick --continue'" }
            "Revert"      { Write-Host "'git revert --continue'" }
        }

        Write-Host ""
        if (Confirm-Action "Islemi IPTAL edip onceki duruma donmek (abort) ister misiniz?" $false) {
            $AbortArgs = switch ($Op) {
                "Merge"       { @("merge", "--abort") }
                "Rebase"      { @("rebase", "--abort") }
                "Cherry-pick" { @("cherry-pick", "--abort") }
                "Revert"      { @("revert", "--abort") }
            }

            & git @AbortArgs | Out-Host

            if ($LASTEXITCODE -eq 0) {
                Write-Ok "Islem iptal edildi."
                $StashEntries = @(git stash list 2>$null)
                if ($StashEntries.Count -gt 0) {
                    Write-Warn "Not: Stash'te kayit var ('git stash list'). Gerekirse 'git stash pop' ile geri alin."
                }
            }
            else { Write-Err "Abort basarisiz." }
        }
    }

    Wait-Menu
}

# ------------------------------------------------------------
# 14 - COMMIT GECMISI
# ------------------------------------------------------------

function Show-CommitHistory {
    Write-Banner "COMMIT GECMISI"
    git --no-pager log --graph --decorate --oneline --all -30
    Wait-Menu
}

# ------------------------------------------------------------
# 15 - BRANCH ISLEMLERI
# ------------------------------------------------------------

function Show-BranchMenu {
    while ($true) {
        Write-Banner "BRANCH ISLEMLERI"

        $Info = Get-BranchInfo
        Write-Host "Mevcut branch: $($Info.Branch)" -ForegroundColor Green
        Write-Host ""
        Write-Host "  [1] Branchleri listele"
        Write-Host "  [2] Branch degistir"
        Write-Host "  [3] Yeni branch olustur"
        Write-Host "  [0] Geri"
        Write-Host ""

        $S = Read-Host "Seciminiz"

        switch ($S) {
            "1" {
                Write-Host ""
                git --no-pager branch -vv
                Wait-Menu
            }
            "2" {
                $Local = @(git for-each-ref --format="%(refname:short)" refs/heads 2>$null)
                Write-Host ""
                for ($i = 0; $i -lt $Local.Count; $i++) { Write-Host ("[{0}] {1}" -f ($i + 1), $Local[$i]) }
                Write-Host ""

                $Pick = (Read-Host "Numara veya branch adi (Enter = iptal)").Trim()
                if ($Pick -eq "") { continue }

                $Name = $Pick
                if ($Pick -match '^\d+$') {
                    $Ix = [int]$Pick - 1
                    if ($Ix -ge 0 -and $Ix -lt $Local.Count) { $Name = $Local[$Ix] }
                    else { Write-Err "Gecersiz numara."; Wait-Menu; continue }
                }

                if (@(Get-LocalChanges).Count -gt 0) {
                    Write-Warn "Commit edilmemis degisiklikler var; cakisma olursa git gecisi reddeder."
                }

                git switch $Name | Out-Host
                Wait-Menu
            }
            "3" {
                $Name = (Read-Host "Yeni branch adi (Enter = iptal)").Trim()
                if ($Name -eq "") { continue }

                git check-ref-format --branch $Name *> $null
                if ($LASTEXITCODE -ne 0) {
                    Write-Err "Gecersiz branch adi (bosluk ve ozel karakter kullanmayin)."
                    Wait-Menu
                    continue
                }

                git switch -c $Name | Out-Host
                if ($LASTEXITCODE -eq 0) {
                    Write-Ok "Branch olusturuldu. GitHub'a gondermek icin 6 numarali secenegi kullanin."
                }
                Wait-Menu
            }
            "0" { return }
            default { Write-Err "Gecersiz secim."; Start-Sleep -Seconds 1 }
        }
    }
}

# ------------------------------------------------------------
# 16 - .GITIGNORE DENETIMI
# ------------------------------------------------------------

function Test-GitIgnoreSetup {
    Write-Banner ".GITIGNORE DENETIMI"

    $Checks = @(
        @{ Probe = "bin/_probe.dll";           Line = "bin/" },
        @{ Probe = "obj/_probe.dll";           Line = "obj/" },
        @{ Probe = ".vs/_probe";               Line = ".vs/" },
        @{ Probe = "node_modules/_probe.js";   Line = "node_modules/" },
        @{ Probe = "_probe.csproj.user";       Line = "*.user" },
        @{ Probe = "_probe.log";               Line = "*.log" }
    )

    # Yedek klasoru repo icindeyse onu da kontrol et
    if ($script:BackupRoot.StartsWith($script:RepoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        $Rel = (Split-Path $script:BackupRoot -Leaf)
        $Checks += @{ Probe = "$Rel/_probe"; Line = "$Rel/" }
    }

    $Missing = @()
    foreach ($C in $Checks) {
        git check-ignore -q $C.Probe 2>$null
        if ($LASTEXITCODE -ne 0) { $Missing += $C.Line }
    }

    if ($Missing.Count -eq 0) {
        Write-Ok ".gitignore temel kurallari tamam."
    }
    else {
        Write-Warn "Eksik .gitignore kurallari:"
        foreach ($M in $Missing) { Write-Warn "  $M" }
        Write-Host ""

        if (Confirm-Action ".gitignore dosyasina eklensin mi?" $true) {
            $GiPath = Join-Path $script:RepoRoot ".gitignore"
            $Text = ""

            if (Test-Path $GiPath) {
                $Existing = [System.IO.File]::ReadAllText($GiPath)
                if ($Existing.Length -gt 0 -and -not $Existing.EndsWith("`n")) { $Text += "`r`n" }
            }

            $Text += "# GAMABEL otomatik eklenenler`r`n" + (($Missing -join "`r`n") + "`r`n")
            [System.IO.File]::AppendAllText($GiPath, $Text, (New-Object System.Text.UTF8Encoding($false)))

            Write-Ok ".gitignore guncellendi. Degisikligi 6 numarali secenekle commit edebilirsiniz."
        }
    }

    # Zaten takip edilen ama ignore edilmesi gereken dosyalar
    $Tracked = @(git -c core.quotepath=false ls-files 2>$null | Where-Object { $_ -match '(^|/)(bin|obj|\.vs|node_modules)/' })

    if ($Tracked.Count -gt 0) {
        Write-Host ""
        Write-Err "UYARI: Repoda zaten takip edilen derleme/IDE dosyalari var: $($Tracked.Count) adet"
        $Tracked | Select-Object -First 8 | ForEach-Object { Write-Host "  $_" }
        Write-Host ""
        Write-Warn "Takipten cikarmak icin (dosyalar diskte kalir):"
        Write-Host "  git rm -r --cached bin obj .vs   (ilgili klasorler icin)"
        Write-Host "  sonra commit edin."
    }

    Wait-Menu
}

# ------------------------------------------------------------
# 17 - HIZLI GUNCELLE  (gitguncelle.ps1 akisi)
#      internet > pull > duzenle > commit > push > log + ozet
# ------------------------------------------------------------

function Invoke-QuickUpdate {
    Write-Banner "HIZLI GUNCELLE  (Pull > Duzenle > Commit > Push)"

    $Start = Get-Date

    Write-Host ">> Proje Dizini: $script:RepoRoot" -ForegroundColor Magenta
    Write-Host ""

    $Info = Get-BranchInfo
    $Last = git --no-pager log -1 --oneline 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace("$Last")) { $Last = "Henuz commit yok" }

    Write-Warn "Guncel Dal: $($Info.Branch)"
    Write-Warn "Son Kayit : $Last"
    Write-Host ""

    # 1) Internet. Yumusak kontrol: proxy/VPN'de HTTP basarisiz olsa da git calisabilir.
    Write-Warn ">> Internet baglantisi kontrol ediliyor..."
    if (Test-InternetConnection) {
        Write-Ok "Internet baglantisi var."
    }
    else {
        Write-Err "github.com'a HTTP ile ulasilamadi."
        if (-not (Confirm-Action "Yine de GitHub baglantisi denensin mi?" $false)) {
            Write-Warn "Islem iptal edildi."
            Wait-Menu
            return
        }
    }

    # 2) Pull (fetch + guvenli fast-forward; kirli calisma alani stash ile korunur)
    $Ctx = Initialize-SyncContext
    if ($null -eq $Ctx) { Wait-Menu; return }

    Write-Host ""

    if (-not $Ctx.HasUpstream) {
        Write-Warn "Upstream yok: cekilecek bir sey yok (ilk push'ta otomatik tanimlanir)."
    }
    elseif ($Ctx.Behind -gt 0 -and $Ctx.Ahead -gt 0) {
        Write-Err "Lokal ve GitHub AYRISMIS. Hizli guncelleme calistirilmadi."
        Write-Warn "8 numarali Akilli Senkronizasyon'u kullanin."
        Wait-Menu
        return
    }
    elseif ($Ctx.Behind -gt 0) {
        Write-Info "GitHub'dan alinacak commitler:"
        git --no-pager log --oneline "HEAD..@{u}"
        Write-Host ""

        if (@(Get-LocalChanges).Count -gt 0) {
            Write-Warn "Lokal degisiklikler var: gecici stash'e alinip guncelleme sonrasi geri yuklenecek."
            if (Confirm-Action "Guvenlik yedegi alinsin mi?" $true) {
                if (-not (New-ProjectBackup)) { Write-Err "Yedek basarisiz, iptal."; Wait-Menu; return }
            }
        }

        if (-not (Confirm-Action "Bu guncellemeler lokale uygulansin mi?" $true)) {
            Write-Warn "Islem iptal edildi."
            Wait-Menu
            return
        }

        if (-not (Invoke-FastForward)) {
            Write-Err "Pull tamamlanamadi."
            Wait-Menu
            return
        }

        Write-Ok "Pull tamamlandi."
    }
    else {
        Write-Ok "GitHub'da yeni guncelleme yok, lokal guncel."
    }

    # 3) Kullanici kodu duzenler
    Write-Host ""
    Write-Warn "Kod duzenlemelerini tamamladiysaniz Enter'a basin (vazgecmek icin 'iptal' yazin)..."
    $Wait = Read-Host

    if ($Wait -ieq "iptal") {
        Write-Warn "Islem iptal edildi."
        Wait-Menu
        return
    }

    # 4) Degisiklik listesi, risk/secret taramasi, build/test, commit, push, log, ozet
    Send-ToRemote -StartTime $Start
}

# ------------------------------------------------------------
# BASLANGIC
# ------------------------------------------------------------

if (-not (Test-Git)) { Wait-Menu; exit 1 }
if (-not (Test-GitRepository)) { Wait-Menu; exit 1 }

# Repo kokune gec: 'git add -A' ve status tum repoyu kapsar.
$script:RepoRoot = ((git rev-parse --show-toplevel) | Select-Object -First 1).Trim().Replace("/", "\")
Set-Location $script:RepoRoot

$script:GitDir = ((git rev-parse --absolute-git-dir) | Select-Object -First 1).Trim().Replace("/", "\")

$RepoName = Split-Path $script:RepoRoot -Leaf
$BackupParent = Split-Path $script:RepoRoot -Parent

if ([string]::IsNullOrWhiteSpace($BackupParent)) {
    $script:BackupRoot = Join-Path $script:RepoRoot "_GAMABEL_BACKUPS"
}
else {
    $script:BackupRoot = Join-Path $BackupParent "${RepoName}_GAMABEL_BACKUPS"
}

# Islem logu repo disinda tutulur: 'git add -A' ile yanlislikla commit'e girmez.
if ([string]::IsNullOrWhiteSpace($BackupParent)) {
    $script:LogFile = Join-Path $script:RepoRoot "git_guncelleme_log.txt"
}
else {
    $script:LogFile = Join-Path $BackupParent "${RepoName}_git_guncelleme_log.txt"
}

# .\Gitupdate.ps1 -Hizli  -> menu acilmadan dogrudan hizli akis
if ($Hizli) {
    Invoke-QuickUpdate
    exit 0
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
    Write-Host "  [11] Yedekten Geri Yukle" -ForegroundColor White
    Write-Host "  [12] VS Code'da Projeyi Ac" -ForegroundColor White
    Write-Host "  [13] Conflict / Yarim Islem Kontrolu" -ForegroundColor White
    Write-Host "  [14] Commit Gecmisini Goster" -ForegroundColor White
    Write-Host "  [15] Branch Islemleri" -ForegroundColor White
    Write-Host "  [16] .gitignore Denetimi" -ForegroundColor White
    Write-Host ""
    Write-Host "  [17] HIZLI GUNCELLE (Pull > Duzenle > Commit > Push)" -ForegroundColor Green
    Write-Host "  [18] Tum Veritabanini Yedekle" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  [0]  Cikis" -ForegroundColor Red
    Write-Host ""

    $Secim = (Read-Host "Seciminiz").Trim()

    switch ($Secim) {
        "1"  { Start-Application }
        "2"  { Invoke-ApplicationBuild }
        "3"  { Test-Application }
        "4"  { Show-GitStatus }
        "5"  { Update-FromRemote }
        "6"  { Send-ToRemote }
        "7"  { Show-RemoteStatus }
        "8"  { Invoke-SmartSync }
        "9"  { Show-DetailedChanges }
        "10" { Backup-Menu }
        "11" { Restore-ProjectBackup }
        "12" { Open-VSCode }
        "13" { Show-ConflictCheck }
        "14" { Show-CommitHistory }
        "15" { Show-BranchMenu }
        "16" { Test-GitIgnoreSetup }
        "17" { Invoke-QuickUpdate }
        "18" { Backup-Database }

        "0" {
            Clear-Host
            Write-Host ""
            Write-Ok "GAMABEL MVC Gelistirme Merkezi kapatildi."
            Write-Host ""
            exit 0
        }

        default {
            Write-Err "Gecersiz secim."
            Start-Sleep -Seconds 1
        }
    }
}
