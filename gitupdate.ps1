#Requires -Version 5.1
# ============================================================
# GAMABEL MVC - GELISTIRME YONETIM MERKEZI  (v2)
# ============================================================
# Menu:
#  1  Uygulamayi calistir            9  Degisiklikleri detayli incele
#  2  Projeyi build et              10  Proje yedegi al
#  3  Testleri calistir             11  Yedekten geri yukle (secmeli)
#  4  Git durumunu goster           12  VS Code'da ac
#  5  GitHub -> Lokal (ff-only)     13  Conflict / yarim islem kontrolu
#  6  Lokal -> GitHub               14  Commit gecmisi
#  7  GitHub durumunu kontrol et    15  Branch islemleri
#  8  Akilli senkronizasyon         16  .gitignore denetimi
#  0  Cikis
#
# v2 ile gelen duzeltmeler:
#  - Log dosyasi .git icine tasindi (artik "lokal degisiklik" yaratmaz)
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
#   { "BackupKeep": 15, "AsciiCommitMessages": true }
# ============================================================

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

function Pause-Menu {
    Write-Host ""
    Read-Host "Devam etmek icin Enter'a basin" | Out-Null
}

function Write-Log {
    param([string]$Mesaj)
    try {
        if ($script:LogFile) {
            $Kayit = "[{0}] {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Mesaj
            Add-Content -Path $script:LogFile -Value $Kayit -Encoding UTF8
        }
    }
    catch {}
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
    git -c core.quotepath=false status --short -uall
    Write-Host ""
    Write-Warn "Toplam: $($Changes.Count) degisiklik"
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
            Write-Log "Build basarisiz (push oncesi)."
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
        Write-Log "Testler basarisiz (push oncesi)."
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

    if (-not (Test-DotNet)) { Pause-Menu; return }

    $Project = Get-RunProject
    if ($null -eq $Project) { Pause-Menu; return }

    Write-Ok "Proje: $($Project.FullName)"
    Write-Log "Uygulama calistirildi: $($Project.FullName)"

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

    Pause-Menu
}

# ------------------------------------------------------------
# 2 - BUILD
# ------------------------------------------------------------

function Build-Application {
    Write-Banner "PROJE BUILD"

    if (-not (Test-DotNet)) { Pause-Menu; return }

    $Target = Get-BuildTarget
    if ($null -eq $Target) { Pause-Menu; return }

    if (Invoke-ProjectBuild $Target) {
        Write-Ok "BUILD BASARILI."
        Write-Log "Build basarili: $($Target.Name)"
    }
    else {
        Write-Err "BUILD BASARISIZ."
        Write-Log "Build basarisiz: $($Target.Name)"
    }

    Pause-Menu
}

# ------------------------------------------------------------
# 3 - TEST
# ------------------------------------------------------------

function Test-Application {
    Write-Banner "TESTLER"

    if (-not (Test-DotNet)) { Pause-Menu; return }

    $Result = Invoke-ProjectTests

    if ($null -eq $Result) { Write-Warn "Test projesi bulunamadi." }
    elseif ($Result) { Write-Ok "TESTLER BASARILI."; Write-Log "Testler basarili" }
    else { Write-Err "TESTLER BASARISIZ."; Write-Log "Testler basarisiz" }

    Pause-Menu
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

    Pause-Menu
}

# ------------------------------------------------------------
# 5 - GITHUB -> LOKAL
# ------------------------------------------------------------

function Update-FromRemote {
    Write-Banner "GITHUB GUNCELLEMELERINI LOKALE UYGULA"

    $Ctx = Initialize-SyncContext
    if ($null -eq $Ctx) { Pause-Menu; return }

    $Changes = @(Get-LocalChanges)

    if (-not $Ctx.HasUpstream) {
        Write-Warn "Bu branch icin upstream (GitHub karsiligi) tanimli degil."
        Write-Warn "6 numarali secenek branch'i GitHub'a ilk kez gonderebilir."
        Pause-Menu
        return
    }

    Write-Host ""
    Write-Warn ("GitHub'da yeni commit  : {0}" -f $Ctx.Behind)
    Write-Warn ("Lokal gonderilmemis    : {0}" -f $Ctx.Ahead)
    Write-Warn ("Lokal dosya degisikligi: {0}" -f $Changes.Count)

    if ($Ctx.Behind -eq 0) {
        Write-Host ""
        Write-Ok "GitHub'da uygulanacak yeni guncelleme yok."
        Pause-Menu
        return
    }

    if ($Ctx.Ahead -gt 0) {
        Write-Host ""
        Write-Err "Lokal ve GitHub AYRISMIS (her iki tarafta farkli commitler var)."
        Write-Warn "Fast-forward mumkun degil. 8 numarali Akilli Senkronizasyon'u kullanin."
        Pause-Menu
        return
    }

    Write-Host ""
    Write-Info "GitHub'daki yeni commitler:"
    git --no-pager log --oneline "HEAD..@{u}"
    Write-Host ""

    if (-not (Confirm-Action "Bu guncellemeler lokale uygulansin mi?" $true)) {
        Write-Warn "Islem iptal edildi."
        Pause-Menu
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
                Pause-Menu
                return
            }
        }

        if (-not (Confirm-Action "Devam edilsin mi?" $true)) {
            Write-Warn "Islem iptal edildi."
            Pause-Menu
            return
        }
    }

    if (Invoke-FastForward) {
        Write-Ok "GITHUB GUNCELLEMELERI LOKALE BASARIYLA UYGULANDI."
        Write-Log "GitHub -> Lokal basarili. Branch: $($Ctx.Branch)"
    }
    else {
        Write-Err "GUNCELLEME TAMAMLANAMADI."
        Write-Warn "Mevcut kodunuz zorla ezilmedi."
        Write-Log "GitHub -> Lokal basarisiz. Branch: $($Ctx.Branch)"
    }

    Pause-Menu
}

# ------------------------------------------------------------
# 6 - LOKAL -> GITHUB
# ------------------------------------------------------------

function Send-ToRemote {
    Write-Banner "LOKAL DEGISIKLIKLERI GITHUB'A YUKLE"

    $Ctx = Initialize-SyncContext
    if ($null -eq $Ctx) { Pause-Menu; return }

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
        Pause-Menu
        return
    }

    # --- Sadece daha once commit edilmis ama gonderilmemis isler ---
    if ($Changes.Count -eq 0) {
        if ($Ctx.HasUpstream -and $Ctx.Ahead -eq 0) {
            Write-Host ""
            Write-Ok "GitHub'a gonderilecek bir sey yok."
            Pause-Menu
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
            Pause-Menu
            return
        }

        if (-not (Invoke-PreCommitChecks)) { Write-Warn "Push iptal edildi."; Pause-Menu; return }

        if (Invoke-Push $Ctx) {
            Write-Ok "GITHUB'A BASARIYLA YUKLENDI."
            Write-Log "Push basarili (sadece commitler). Branch: $($Ctx.Branch)"
        }
        else {
            Write-Err "PUSH BASARISIZ. Commitler lokalde duruyor."
            Write-Log "Push basarisiz. Branch: $($Ctx.Branch)"
        }

        Pause-Menu
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
            Pause-Menu
            return
        }
    }

    if (-not (Test-GitIdentity)) { Pause-Menu; return }

    $HadStaged = (@($Changes | Where-Object { $_.Staged }).Count -gt 0)

    Write-Host ""
    Write-Warn "Degisiklikler stage'e aliniyor (git add -A)..."
    git add -A

    if ($LASTEXITCODE -ne 0) {
        Write-Err "git add basarisiz."
        Pause-Menu
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
            Pause-Menu
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
        Pause-Menu
        return
    }

    # Commit mesaji (bos birakilamaz, 'iptal' yazilirsa cikilir)
    do {
        Write-Host ""
        $Mesaj = Read-Host "Commit mesaji (iptal icin 'iptal' yazin)"

        if ($Mesaj -ieq "iptal") {
            Undo-Stage $HadStaged
            Write-Warn "Islem iptal edildi."
            Pause-Menu
            return
        }

        if ([string]::IsNullOrWhiteSpace($Mesaj)) { Write-Err "Commit mesaji bos olamaz." }
    }
    while ([string]::IsNullOrWhiteSpace($Mesaj))

    $Mesaj = $Mesaj.Trim()
    if ($Config.AsciiCommitMessages) { $Mesaj = ConvertTo-LatinChars $Mesaj }

    if (-not (Invoke-Commit $Mesaj)) {
        Write-Err "Commit basarisiz."
        Pause-Menu
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
        Pause-Menu
        return
    }

    $After = Get-BranchInfo

    if ($After.HasUpstream -and $After.Behind -gt 0) {
        Write-Host ""
        Write-Err "DURUM DEGISTI: GitHub'a yeni commit gelmis! Guvenlik nedeniyle push yapilmadi."
        Write-Warn "Commit lokalde kayitli. 8 numarali Akilli Senkronizasyon'u kullanin."
        Pause-Menu
        return
    }

    if (-not (Confirm-Action "Commit GitHub'a push edilsin mi?" $true)) {
        Write-Warn "Push yapilmadi. Commit lokalde kayitli."
        Pause-Menu
        return
    }

    if (Invoke-Push $After) {
        Write-Ok "DEGISIKLIKLER GITHUB'A BASARIYLA YUKLENDI."
        Write-Log "Lokal -> GitHub push basarili. Branch: $($After.Branch) | Commit: $Mesaj"
    }
    else {
        Write-Err "PUSH BASARISIZ. Commit lokalde kayitli, daha sonra tekrar deneyebilirsiniz."
        Write-Log "Lokal -> GitHub push basarisiz. Branch: $($After.Branch) | Commit: $Mesaj"
    }

    Pause-Menu
}

# ------------------------------------------------------------
# 7 - GITHUB DURUMU
# ------------------------------------------------------------

function Show-RemoteStatus {
    Write-Banner "GITHUB DURUMU"

    $Ctx = Initialize-SyncContext
    if ($null -eq $Ctx) { Pause-Menu; return }

    $Changes = @(Get-LocalChanges)

    Write-Host ""
    Write-Host ("Branch                 : {0}" -f $Ctx.Branch)

    if (-not $Ctx.HasUpstream) {
        Write-Warn "Upstream               : yok (branch GitHub'a gonderilmemis)"
        Write-Warn ("Commit edilmemis dosya : {0}" -f $Changes.Count)
        Write-Host ""
        Write-Warn "6 numarali secenek branch'i GitHub'a ilk kez gonderebilir."
        Pause-Menu
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

    Pause-Menu
}

# ------------------------------------------------------------
# 8 - AKILLI SENKRONIZASYON
# ------------------------------------------------------------

function Invoke-SmartSync {
    Write-Banner "AKILLI SENKRONIZASYON"

    $Ctx = Initialize-SyncContext
    if ($null -eq $Ctx) { Pause-Menu; return }

    $Changes = @(Get-LocalChanges)
    $Dirty = $Changes.Count -gt 0

    if (-not $Ctx.HasUpstream) {
        Write-Warn "Bu branch icin upstream yok. 6 numarali secenekle GitHub'a gonderin."
        Pause-Menu
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
        Pause-Menu
        return
    }

    # 2) Sadece commit edilmemis degisiklik
    if ($Ctx.Behind -eq 0 -and $Ctx.Ahead -eq 0 -and $Dirty) {
        Write-Warn "Sadece commit edilmemis lokal degisiklikler var."
        Show-LocalChanges
        Write-Host ""
        Write-Ok "6 numarali secenekle commit + push yapabilirsiniz."
        Pause-Menu
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
                if (-not (New-ProjectBackup)) { Write-Err "Yedek basarisiz, iptal."; Pause-Menu; return }
            }
        }

        if (Confirm-Action "GitHub guncellemeleri lokale uygulansin mi?" $true) {
            if (Invoke-FastForward) {
                Write-Ok "Senkronizasyon tamamlandi."
                Write-Log "Akilli senkronizasyon: GitHub -> Lokal"
            }
            else { Write-Err "Guncelleme tamamlanamadi." }
        }

        Pause-Menu
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
                Write-Log "Akilli senkronizasyon: Lokal -> GitHub"
            }
            else { Write-Err "Push basarisiz." }
        }

        Pause-Menu
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
        Pause-Menu
        return
    }

    if ($Dirty) {
        Write-Warn "Lokal degisiklikler stash ile korunacak."
        if (Confirm-Action "Guvenlik yedegi alinsin mi?" $true) {
            if (-not (New-ProjectBackup)) { Write-Err "Yedek basarisiz, iptal."; Pause-Menu; return }
        }
    }

    if (-not (Confirm-Action "Devam edilsin mi?" $true)) {
        Write-Warn "Islem iptal edildi."
        Pause-Menu
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
        Write-Log "Akilli senkronizasyon: ayrisma cozumu basarisiz."
        Pause-Menu
        return
    }

    Write-Ok "Birlestirme basarili."
    Write-Log "Akilli senkronizasyon: ayrisma cozuldu ($Secim)."

    if (Confirm-Action "Simdi build/test calistirip GitHub'a push edelim mi?" $true) {
        if (Invoke-PreCommitChecks) {
            $Fresh = Get-BranchInfo
            if (Invoke-Push $Fresh) { Write-Ok "GitHub'a yuklendi." }
            else { Write-Err "Push basarisiz." }
        }
    }

    Pause-Menu
}

# ------------------------------------------------------------
# 9 - DETAYLI DEGISIKLIKLER
# ------------------------------------------------------------

function Show-DetailedChanges {
    Write-Banner "DEGISIKLIKLERI INCELE"

    $Changes = @(Get-LocalChanges)

    if ($Changes.Count -eq 0) {
        Write-Ok "Degisiklik yok."
        Pause-Menu
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

    Pause-Menu
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
        Write-Log "Proje yedegi alindi: $Dest"

        Remove-OldBackups -Protect $Protect
        return $true
    }
    catch {
        Write-Err "YEDEK ALINAMADI: $($_.Exception.Message)"
        Write-Log "Proje yedegi basarisiz: $($_.Exception.Message)"
        return $false
    }
}

function Backup-Menu {
    Write-Banner "PROJE YEDEGI"
    Write-Host "Yedek konumu: $script:BackupRoot" -ForegroundColor Gray
    Write-Host "Saklanan yedek sayisi: $($Config.BackupKeep)" -ForegroundColor Gray
    Write-Host ""
    New-ProjectBackup | Out-Null
    Pause-Menu
}

# ------------------------------------------------------------
# 11 - YEDEKTEN GERI YUKLE
# ------------------------------------------------------------

function Restore-ProjectBackup {
    Write-Banner "YEDEKTEN GERI YUKLE"

    if (-not (Test-Path $script:BackupRoot)) {
        Write-Warn "Yedek klasoru bulunamadi."
        Pause-Menu
        return
    }

    $Backups = @(Get-ChildItem -Path $script:BackupRoot -Directory | Sort-Object Name -Descending | Select-Object -First 15)

    if ($Backups.Count -eq 0) {
        Write-Warn "Yedek bulunamadi."
        Pause-Menu
        return
    }

    Write-Info "Mevcut yedekler (en yeni ustte):"
    $Selected = Select-Item -Items $Backups -Label { param($d) $d.Name } -Prompt "Geri yuklenecek yedegin numarasi (Enter = iptal)" -Always

    if ($null -eq $Selected) {
        Write-Warn "Geri yukleme iptal edildi."
        Pause-Menu
        return
    }

    Write-Host ""
    Write-Info "Secilen yedek: $($Selected.FullName)"
    Write-Err "DIKKAT: Mevcut proje dosyalarinin uzerine yazilacak."

    $Mirror = Confirm-Action "Yedekte OLMAYAN dosyalar da silinsin mi? (tam ayna, riskli)" $false

    $Onay = Read-Host "Onaylamak icin GERI YUKLE yazin"
    if ($Onay -cne "GERI YUKLE") {
        Write-Warn "Geri yukleme iptal edildi."
        Pause-Menu
        return
    }

    Write-Warn "Once mevcut durumun guvenlik yedegi aliniyor..."
    if (-not (New-ProjectBackup -Suffix "restore-oncesi" -Protect $Selected.FullName)) {
        Write-Err "Guvenlik yedegi alinamadi. Geri yukleme iptal edildi."
        Pause-Menu
        return
    }

    try {
        $Mode = if ($Mirror) { "/MIR" } else { "/E" }
        $Xd = @(".git", "bin", "obj", ".vs", "node_modules", $script:BackupRoot)
        $RoboArgs = @($Selected.FullName, $script:RepoRoot, $Mode, "/XD") + $Xd + @("/NFL", "/NDL", "/NJH", "/NJS", "/NP", "/R:1", "/W:1")

        & robocopy @RoboArgs | Out-Null

        if ($LASTEXITCODE -ge 8) { throw "robocopy hata kodu: $LASTEXITCODE" }

        Write-Ok "YEDEK GERI YUKLENDI: $($Selected.Name)"
        Write-Log "Yedek geri yuklendi: $($Selected.FullName) (mirror=$Mirror)"
    }
    catch {
        Write-Err "Geri yukleme basarisiz: $($_.Exception.Message)"
        Write-Log "Geri yukleme basarisiz: $($_.Exception.Message)"
    }

    Pause-Menu
}

# ------------------------------------------------------------
# 12 - VS CODE
# ------------------------------------------------------------

function Open-VSCode {
    Write-Info ">> VS CODE ACILIYOR..."

    if (Get-Command code -ErrorAction SilentlyContinue) {
        code $script:RepoRoot
        Write-Ok "VS Code acildi."
        Write-Log "VS Code acildi."
    }
    else {
        Write-Err "'code' komutu bulunamadi. VS Code PATH ayarini kontrol edin."
    }

    Pause-Menu
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
        Pause-Menu
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
                Write-Log "$Op islemi abort edildi."
                if (@(git stash list 2>$null).Count -gt 0) {
                    Write-Warn "Not: Stash'te kayit var ('git stash list'). Gerekirse 'git stash pop' ile geri alin."
                }
            }
            else { Write-Err "Abort basarisiz." }
        }
    }

    Pause-Menu
}

# ------------------------------------------------------------
# 14 - COMMIT GECMISI
# ------------------------------------------------------------

function Show-CommitHistory {
    Write-Banner "COMMIT GECMISI"
    git --no-pager log --graph --decorate --oneline --all -30
    Pause-Menu
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
                Pause-Menu
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
                    else { Write-Err "Gecersiz numara."; Pause-Menu; continue }
                }

                if (@(Get-LocalChanges).Count -gt 0) {
                    Write-Warn "Commit edilmemis degisiklikler var; cakisma olursa git gecisi reddeder."
                }

                git switch $Name | Out-Host
                if ($LASTEXITCODE -eq 0) { Write-Log "Branch degisti: $Name" }
                Pause-Menu
            }
            "3" {
                $Name = (Read-Host "Yeni branch adi (Enter = iptal)").Trim()
                if ($Name -eq "") { continue }

                git check-ref-format --branch $Name *> $null
                if ($LASTEXITCODE -ne 0) {
                    Write-Err "Gecersiz branch adi (bosluk ve ozel karakter kullanmayin)."
                    Pause-Menu
                    continue
                }

                git switch -c $Name | Out-Host
                if ($LASTEXITCODE -eq 0) {
                    Write-Ok "Branch olusturuldu. GitHub'a gondermek icin 6 numarali secenegi kullanin."
                    Write-Log "Yeni branch: $Name"
                }
                Pause-Menu
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
            Write-Log ".gitignore guncellendi: $($Missing -join ', ')"
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

    Pause-Menu
}

# ------------------------------------------------------------
# BASLANGIC
# ------------------------------------------------------------

if (-not (Test-Git)) { Pause-Menu; exit 1 }
if (-not (Test-GitRepository)) { Pause-Menu; exit 1 }

# Repo kokune gec: 'git add -A' ve status tum repoyu kapsar.
$script:RepoRoot = ((git rev-parse --show-toplevel) | Select-Object -First 1).Trim().Replace("/", "\")
Set-Location $script:RepoRoot

$script:GitDir = ((git rev-parse --absolute-git-dir) | Select-Object -First 1).Trim().Replace("/", "\")
$script:LogFile = Join-Path $script:GitDir "gamabel_log.txt"

$RepoName = Split-Path $script:RepoRoot -Leaf
$BackupParent = Split-Path $script:RepoRoot -Parent

if ([string]::IsNullOrWhiteSpace($BackupParent)) {
    $script:BackupRoot = Join-Path $script:RepoRoot "_GAMABEL_BACKUPS"
}
else {
    $script:BackupRoot = Join-Path $BackupParent "${RepoName}_GAMABEL_BACKUPS"
}

Write-Log "GAMABEL MVC Gelistirme Merkezi baslatildi."

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
    Write-Host "  [0]  Cikis" -ForegroundColor Red
    Write-Host ""

    $Secim = (Read-Host "Seciminiz").Trim()

    switch ($Secim) {
        "1"  { Start-Application }
        "2"  { Build-Application }
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

        "0" {
            Write-Log "Gelistirme Merkezi kapatildi."
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
