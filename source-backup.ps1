param(
    [ValidateSet("backup", "restore", "list")]
    [string]$Action = "backup",

    [string]$BackupName,

    [string]$BackupRoot = "",

    [switch]$SkipSafetyBackup
)

$ErrorActionPreference = "Stop"

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "[OK]   $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Get-ProjectRoot {
    if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        return $PSScriptRoot
    }

    return (Get-Location).Path
}

function Get-BackupRootPath {
    param([string]$Root)

    $projectRoot = Get-ProjectRoot
    if ([string]::IsNullOrWhiteSpace($Root)) {
        return (Join-Path $projectRoot "backups\source")
    }

    if ([System.IO.Path]::IsPathRooted($Root)) {
        return $Root
    }

    return (Join-Path $projectRoot $Root)
}

function Ensure-Directory {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Copy-SourceToTemp {
    param(
        [string]$From,
        [string]$To
    )

    Ensure-Directory -Path $To

    # Build output and internal backup folders are excluded intentionally.
    $excludeDirs = @("bin", "obj", "publish", ".git", ".vs", "backups")

    $roboArgs = @(
        $From,
        $To,
        "/E",
        "/R:1",
        "/W:1",
        "/NFL",
        "/NDL",
        "/NJH",
        "/NJS",
        "/NP"
    )

    foreach ($dir in $excludeDirs) {
        $roboArgs += @("/XD", (Join-Path $From $dir))
    }

    & robocopy @roboArgs | Out-Null
    $exitCode = $LASTEXITCODE
    if ($exitCode -ge 8) {
        throw "Robocopy failed with exit code $exitCode"
    }
}

function New-SourceBackup {
    param([string]$BackupRootPath)

    Ensure-Directory -Path $BackupRootPath

    $projectRoot = Get-ProjectRoot
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $zipName = "source-backup-$stamp.zip"
    $zipPath = Join-Path $BackupRootPath $zipName
    $tempPath = Join-Path ([System.IO.Path]::GetTempPath()) "gamabelmvc-src-$stamp"

    Write-Info "Kaynak dosyalar gecici klasore alinıyor..."
    Copy-SourceToTemp -From $projectRoot -To $tempPath

    Write-Info "Zip olusturuluyor..."
    Compress-Archive -Path (Join-Path $tempPath "*") -DestinationPath $zipPath -CompressionLevel Optimal -Force

    Remove-Item -LiteralPath $tempPath -Recurse -Force

    Write-Ok "Yedek olusturuldu: $zipPath"
    return $zipPath
}

function Get-BackupFiles {
    param([string]$BackupRootPath)

    if (-not (Test-Path -LiteralPath $BackupRootPath)) {
        return @()
    }

    return @(Get-ChildItem -LiteralPath $BackupRootPath -Filter "source-backup-*.zip" -File | Sort-Object LastWriteTime -Descending)
}

function Resolve-RestoreZip {
    param(
        [string]$BackupRootPath,
        [string]$Name
    )

    $files = Get-BackupFiles -BackupRootPath $BackupRootPath
    if ($files.Count -eq 0) {
        throw "Geri yuklemek icin yedek bulunamadi: $BackupRootPath"
    }

    if ([string]::IsNullOrWhiteSpace($Name)) {
        return $files[0].FullName
    }

    $candidate = if ($Name.EndsWith(".zip")) { $Name } else { "$Name.zip" }

    if ([System.IO.Path]::IsPathRooted($candidate) -and (Test-Path -LiteralPath $candidate)) {
        return $candidate
    }

    $pathInBackupRoot = Join-Path $BackupRootPath $candidate
    if (Test-Path -LiteralPath $pathInBackupRoot) {
        return $pathInBackupRoot
    }

    throw "Yedek bulunamadi: $Name"
}

function Clear-ProjectForRestore {
    param([string]$ProjectRoot)

    # Keep folders that should not be removed during restore.
    $keepNames = @(".git", ".vs", "bin", "obj", "publish", "backups")

    Get-ChildItem -LiteralPath $ProjectRoot -Force | ForEach-Object {
        if ($keepNames -contains $_.Name) {
            return
        }

        Remove-Item -LiteralPath $_.FullName -Recurse -Force
    }
}

function Restore-SourceBackup {
    param(
        [string]$BackupRootPath,
        [string]$Name,
        [bool]$CreateSafetyBackup
    )

    $projectRoot = Get-ProjectRoot
    $zipToRestore = Resolve-RestoreZip -BackupRootPath $BackupRootPath -Name $Name

    if ($CreateSafetyBackup) {
        Write-Warn "Geri yukleme oncesi guvenlik yedegi aliniyor..."
        [void](New-SourceBackup -BackupRootPath $BackupRootPath)
    }

    Write-Info "Proje dosyalari geri yukleme icin temizleniyor..."
    Clear-ProjectForRestore -ProjectRoot $projectRoot

    Write-Info "Yedek aciliyor: $zipToRestore"
    Expand-Archive -Path $zipToRestore -DestinationPath $projectRoot -Force

    Write-Ok "Geri yukleme tamamlandi."
}

function Show-Backups {
    param([string]$BackupRootPath)

    $files = Get-BackupFiles -BackupRootPath $BackupRootPath
    if ($files.Count -eq 0) {
        Write-Warn "Yedek bulunamadi: $BackupRootPath"
        return
    }

    Write-Host "Mevcut yedekler:" -ForegroundColor Cyan
    foreach ($file in $files) {
        $sizeMb = [Math]::Round($file.Length / 1MB, 2)
        Write-Host ("- {0} | {1} MB | {2}" -f $file.Name, $sizeMb, $file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"))
    }
}

try {
    $backupRootPath = Get-BackupRootPath -Root $BackupRoot
    Ensure-Directory -Path $backupRootPath

    switch ($Action) {
        "backup" {
            [void](New-SourceBackup -BackupRootPath $backupRootPath)
        }
        "restore" {
            Restore-SourceBackup -BackupRootPath $backupRootPath -Name $BackupName -CreateSafetyBackup:(-not $SkipSafetyBackup)
        }
        "list" {
            Show-Backups -BackupRootPath $backupRootPath
        }
        default {
            throw "Desteklenmeyen Action degeri: $Action"
        }
    }
}
catch {
    Write-Host "[HATA] $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}