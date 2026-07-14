# encoding: utf-8
param(
    [string]$ConfigFile,
    [string]$Method
)

# PowerShell 5.x uyumluluğu için default değerleri elle ata
if (-not $ConfigFile) { $ConfigFile = "deploy-config.json" }
if (-not $Method) { $Method = "" }

# gamabelmvc projesi için otomatik dağıtım (deploy) scripti
# Açıklama: Projeyi derler, yayınlar ve yapılandırmaya göre sunucuya yükler.
# Desteklenen yöntemler: FTP, Dosya Kopyalama (File Copy)
#
# Örnekler:
#   .\deploy.ps1
#   .\deploy.ps1 -Method ftp
#   .\deploy.ps1 -Method filecopy
#   .\deploy.ps1 -ConfigFile "deploy-config.json"

# --- PowerShell 7+ uyumluluk kontrolü ---
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Output "UYARI: Bu script PowerShell 7 veya üzeri ile tam uyumludur. Lütfen PowerShell 7+ kullanın."
}

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$totalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$stepWatch = [System.Diagnostics.Stopwatch]::new()

# --- Yapılandırma Yükle ---
$configPath = Join-Path $ScriptDir $ConfigFile
if (-not (Test-Path $configPath)) {
    Write-Output "HATA: Yapılandırma dosyası bulunamadı: $configPath"
    Write-Output "Lütfen 'deploy-config.json' dosyasını düzenleyin."
    exit 1
}

$config = Get-Content $configPath -Raw | ConvertFrom-Json
$deployMethod = if ($Method -ne "") { $Method } else { $config.DeployMethod }

Write-Output "============================================="
Write-Output "   gamabelmvc - Otomatik Dağıtım Başlıyor"
Write-Output "   Yöntem: $deployMethod"
Write-Output "============================================="
Write-Output ""

# --- 1. Proje Derleme ve Yayınlama ---
$stepWatch.Restart()
Write-Output "[1/4] Proje derleniyor ve yayinlaniyor..."

$publishDir = Join-Path $ScriptDir "publish"

# Önceki yayın klasörünü temizle
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
    # Windows dosya sistemi async silme sorununu onle
    $waitCount = 0
    while ((Test-Path $publishDir) -and $waitCount -lt 10) {
        Start-Sleep -Milliseconds 200
        $waitCount++
    }
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

$buildConfig = $config.Build.Configuration
if (-not $buildConfig) { $buildConfig = "Release" }

$dotnetArgs = @("publish", "gamabelmvc.csproj", "-c", $buildConfig, "-o", $publishDir, "/p:SolutionFilePath=")

if ($config.Build.Runtime -and $config.Build.Runtime -ne "") {
    $dotnetArgs += @("-r", $config.Build.Runtime)
}

if ($config.Build.SelfContained -eq $true) {
    $dotnetArgs += "--self-contained"
} else {
    $dotnetArgs += "--no-self-contained"
}

Write-Output "  Komut: dotnet $($dotnetArgs -join ' ')"
& dotnet @dotnetArgs

if ($LASTEXITCODE -ne 0) {
    Write-Output "HATA: Proje derlemesi başarısız oldu!"
    exit 1
}

Write-Output "[1/4] Proje basariyla yayinlandi ($($stepWatch.Elapsed.TotalSeconds.ToString('0.0'))s)"
Write-Output ""

# --- 2. Degisiklik Tespiti (Hash Karsilastirma) ---
$stepWatch.Restart()
$manifestPath = Join-Path $ScriptDir "deploy-manifest.json"
$previousManifest = @{}
if (Test-Path $manifestPath) {
    $previousManifest = @{}
    $loaded = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $loaded.PSObject.Properties | ForEach-Object { $previousManifest[$_.Name] = $_.Value }
}

$allFiles = Get-ChildItem $publishDir -Recurse -File
$currentManifest = @{}
$changedFiles = @()
$deletedFiles = @()
$newFiles = @()
$modifiedFiles = @()

# .NET MD5 direkt kullanimi (Get-FileHash cmdlet overhead'inden kacin)
$md5 = [System.Security.Cryptography.MD5]::Create()
try {
$savedErrorPref = $ErrorActionPreference
$ErrorActionPreference = "Continue"
foreach ($file in $allFiles) {
    $relativePath = $file.FullName.Substring($publishDir.Length).Replace("\", "/")

    # Dosya hala mevcut mu kontrol et (publish sirasinda .br sikistirma orijinali silebilir)
    if (-not (Test-Path -LiteralPath $file.FullName)) { continue }

    $hash = $null
    $stream = $null
    try {
        $stream = [System.IO.File]::OpenRead($file.FullName)
        $hashBytes = $md5.ComputeHash($stream)
        $hash = [BitConverter]::ToString($hashBytes).Replace("-", "")
    } catch {
        try {
            $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm MD5 -ErrorAction SilentlyContinue).Hash
        } catch {
            Write-Output "HATA: $($file.FullName) için hash alınamadı: $($_.Exception.Message)"
        }
    } finally {
        if ($stream) { $stream.Dispose() }
    }

    if (-not $hash) {
        Write-Output "UYARI: $($file.FullName) için hash üretilemedi, dosya atlandı."
        continue
    }
    $currentManifest[$relativePath] = $hash

    if (-not $previousManifest.ContainsKey($relativePath)) {
        $changedFiles += $file
        $newFiles += $relativePath
    } elseif ($previousManifest[$relativePath] -ne $hash) {
        $changedFiles += $file
        $modifiedFiles += $relativePath
    }
}
$ErrorActionPreference = $savedErrorPref
} finally {
    $ErrorActionPreference = $savedErrorPref
    $md5.Dispose()
}

# Silinen dosyaları bul
foreach ($key in $previousManifest.Keys) {
    if (-not $currentManifest.ContainsKey($key)) {
        $deletedFiles += $key
    }
}

$totalSize = ($allFiles | Measure-Object -Property Length -Sum).Sum
$totalSizeMB = [math]::Round($totalSize / 1MB, 2)
$changedSize = ($changedFiles | Measure-Object -Property Length -Sum).Sum
$changedSizeMB = [math]::Round(($changedSize / 1MB), 2)
$unchangedCount = $allFiles.Count - $changedFiles.Count

Write-Output "[2/4] Toplam: $($allFiles.Count) dosya ($totalSizeMB MB) - Hash: $($stepWatch.Elapsed.TotalSeconds.ToString('0.0'))s"
Write-Output "      Degismemis: $unchangedCount dosya (atlanacak)"
if ($changedFiles.Count -eq 0 -and $deletedFiles.Count -eq 0) {
    Write-Output "      Degisiklik yok, sunucu guncel!"
    # Manifest'i yine de kaydet
    $currentManifest | ConvertTo-Json | Set-Content $manifestPath -Encoding UTF8
    exit 0
}
if ($newFiles.Count -gt 0) {
    Write-Output "      Yeni: $($newFiles.Count) dosya"
    $showNew = if ($newFiles.Count -gt 15) { $newFiles[0..14] } else { $newFiles }
    foreach ($f in $showNew) { Write-Output "        + $f" }
    if ($newFiles.Count -gt 15) { Write-Output "        ... ve $($newFiles.Count - 15) dosya daha" }
}
if ($modifiedFiles.Count -gt 0) {
    Write-Output "      Guncellenen: $($modifiedFiles.Count) dosya"
    $showMod = if ($modifiedFiles.Count -gt 15) { $modifiedFiles[0..14] } else { $modifiedFiles }
    foreach ($f in $showMod) { Write-Output "        ~ $f" }
    if ($modifiedFiles.Count -gt 15) { Write-Output "        ... ve $($modifiedFiles.Count - 15) dosya daha" }
}
if ($deletedFiles.Count -gt 0) {
    Write-Output "      Silinen: $($deletedFiles.Count) dosya"
    $showDel = if ($deletedFiles.Count -gt 15) { $deletedFiles[0..14] } else { $deletedFiles }
    foreach ($f in $showDel) { Write-Output "        - $f" }
    if ($deletedFiles.Count -gt 15) { Write-Output "        ... ve $($deletedFiles.Count - 15) dosya daha" }
}
Write-Output "      Aktarilacak: $($changedFiles.Count) dosya ($changedSizeMB MB)"
Write-Output ""

# --- 3. Sunucuya Yukleme ---
$stepWatch.Restart()
Write-Output "[3/4] Sunucuya yukleme basliyor ($deployMethod)..."

function Initialize-FtpDirectory {
    param($server, $port, $username, $ftpPass, $useSsl, $remoteDirPath, $createdDirs)

    # Zaten oluşturulmuş dizinleri atla
    if ($createdDirs.ContainsKey($remoteDirPath)) { return }

    # Dizin yolunu parçalara ayır ve her seviyeyi tek tek oluştur
    $parts = $remoteDirPath.Trim("/").Split("/")
    $currentPath = ""

    foreach ($part in $parts) {
        $currentPath = "$currentPath/$part"
        if ($createdDirs.ContainsKey($currentPath)) { continue }

        try {
            $ftpDirUri = "ftp://${server}:${port}${currentPath}"
            $ftpDirRequest = [System.Net.FtpWebRequest]::Create($ftpDirUri)
            $ftpDirRequest.Method = [System.Net.WebRequestMethods+Ftp]::MakeDirectory
            $ftpDirRequest.Credentials = New-Object System.Net.NetworkCredential($username, $ftpPass)
            $ftpDirRequest.EnableSsl = $useSsl
            $ftpDirRequest.UsePassive = $true
            $resp = $ftpDirRequest.GetResponse()
            $resp.Close()
        } catch {
            if ($_.Exception.Response.StatusCode -ne 550) {
                Write-Output "Dizin oluşturulamadı: $currentPath - $($_.Exception.Message)"
            }
            # 550 = dizin zaten var, sorun değil
        }
        $createdDirs[$currentPath] = $true
    }
}

function Publish-FTP {
    param($config, $publishDir, $changedFiles, $deletedFiles)

    $ftpConfig = $config.FTP
    $server   = $ftpConfig.Server
    $port     = $ftpConfig.Port
    $username = $ftpConfig.Username
    $password = $ftpConfig.Password
    $remotePath = $ftpConfig.RemotePath
    $useSsl   = $ftpConfig.UseSsl

    if ($server -eq "ftp.siteniz.com" -or [string]::IsNullOrWhiteSpace($server)) {
        Write-Output "HATA: Lutfen deploy-config.json icindeki FTP sunucu bilgilerini guncelleyin!"
        exit 1
    }

    if ([string]::IsNullOrWhiteSpace($password)) {
        $securePass = Read-Host "FTP sifrenizi girin" -AsSecureString
        $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePass)
        $password = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }

    $protocol = if ($useSsl) { "ftps" } else { "ftp" }
    Write-Output "  Sunucu: ${protocol}://${server}:${port}${remotePath}"

    # --- app_offline.htm yukle (uygulamayi durdurur, DLL kilidini acar) ---
    Write-Output "  app_offline.htm yukleniyor (uygulama durduruluyor)..."
    try {
        $offlineContent = [System.Text.Encoding]::UTF8.GetBytes("<html><body><h1>Uygulama guncelleniyor, lutfen bekleyin...</h1></body></html>")
        $offlineUri = "ftp://${server}:${port}$($remotePath.TrimEnd('/'))/app_offline.htm"
        $offlineReq = [System.Net.FtpWebRequest]::Create($offlineUri)
        $offlineReq.Method = [System.Net.WebRequestMethods+Ftp]::UploadFile
        $offlineReq.Credentials = New-Object System.Net.NetworkCredential($username, $password)
        $offlineReq.EnableSsl = $useSsl
        $offlineReq.UseBinary = $true
        $offlineReq.UsePassive = $true
        $offlineReq.ContentLength = $offlineContent.Length
        $offlineStream = $offlineReq.GetRequestStream()
        $offlineStream.Write($offlineContent, 0, $offlineContent.Length)
        $offlineStream.Close()
        $offlineResp = $offlineReq.GetResponse()
        $offlineResp.Close()
        Write-Output "  app_offline.htm yuklendi, uygulama durduruluyor..."
        Start-Sleep -Seconds 2
    } catch {
        Write-Output "  UYARI: app_offline.htm yuklenemedi: $($_.Exception.Message)"
        if ($_.Exception.InnerException) {
            Write-Output "  Detay: $($_.Exception.InnerException.Message)"
        }
    }

    $createdDirs = @{}
    $failedFiles = @()

    # Degisen dosyalarin dizinlerini olustur
    if ($changedFiles.Count -gt 0) {
        Write-Output "  Dizinler kontrol ediliyor..."
        $dirsNeeded = @{}
        foreach ($file in $changedFiles) {
            $relDir = (Split-Path $file.FullName.Substring($publishDir.Length) -Parent).Replace("\", "/")
            if ($relDir -and -not $dirsNeeded.ContainsKey($relDir)) {
                $dirsNeeded[$relDir] = $true
                $remoteDir = $remotePath.TrimEnd("/") + $relDir
                Initialize-FtpDirectory -server $server -port $port -username $username -ftpPass $password -useSsl $useSsl -remoteDirPath $remoteDir -createdDirs $createdDirs
            }
        }
    }

    # Paralel FTP yuklemesi (Runspace Pool)
    $parallelCount = if ($config.FTP.ParallelUploads) { [int]$config.FTP.ParallelUploads } else { 5 }
    Write-Output "  Paralel yukleme: $parallelCount es zamanli baglanti"

    $totalFiles = $changedFiles.Count

    $uploadScript = {
        param($ftpServer, $ftpPort, $ftpUser, $ftpPass, $ftpSsl, $remoteFilePath, $localPath, $relPath)
        try {
            $ftpUri = "ftp://${ftpServer}:${ftpPort}${remoteFilePath}"
            $req = [System.Net.FtpWebRequest]::Create($ftpUri)
            $req.Method = [System.Net.WebRequestMethods+Ftp]::UploadFile
            $req.Credentials = New-Object System.Net.NetworkCredential($ftpUser, $ftpPass)
            $req.EnableSsl = $ftpSsl
            $req.UseBinary = $true
            $req.UsePassive = $true
            $req.KeepAlive = $true

            $bytes = [System.IO.File]::ReadAllBytes($localPath)
            $req.ContentLength = $bytes.Length

            $stream = $req.GetRequestStream()
            $stream.Write($bytes, 0, $bytes.Length)
            $stream.Close()

            $resp = $req.GetResponse()
            $resp.Close()
            return @{ OK = $true; Path = $relPath }
        } catch {
            return @{ OK = $false; Path = $relPath; Error = $_.Exception.Message; LocalPath = $localPath }
        }
    }

    $runspacePool = [runspacefactory]::CreateRunspacePool(1, $parallelCount)
    $runspacePool.Open()

    $jobs = @()
    foreach ($file in $changedFiles) {
        $relativePath = $file.FullName.Substring($publishDir.Length).Replace("\", "/")
        $remoteFilePath = $remotePath.TrimEnd("/") + $relativePath

        $ps = [powershell]::Create().AddScript($uploadScript)
        $ps.AddArgument($server).AddArgument($port).AddArgument($username).AddArgument($password) | Out-Null
        $ps.AddArgument($useSsl).AddArgument($remoteFilePath).AddArgument($file.FullName).AddArgument($relativePath) | Out-Null
        $ps.RunspacePool = $runspacePool

        $jobs += @{
            PS = $ps
            Handle = $ps.BeginInvoke()
            RelPath = $relativePath
            File = $file
        }
    }

    # Sonuclari topla
    $completed = 0
    foreach ($job in $jobs) {
        $result = $job.PS.EndInvoke($job.Handle)
        $job.PS.Dispose()
        $completed++

        if ($totalFiles -gt 0) {
            $percent = [math]::Round(($completed / $totalFiles) * 100)
            Write-Progress -Activity "FTP Yukleme ($parallelCount paralel)" -Status "$completed / $totalFiles" -PercentComplete $percent
        }

        if ($result -and $result.Count -gt 0) {
            $r = $result[0]
            if (-not $r.OK) {
                $failedFiles += @{ Path = $r.Path; Error = $r.Error; File = $job.File }
                Write-Output "  UYARI: $($r.Path) yuklenemedi: $($r.Error)"
            }
        }
    }

    Write-Progress -Activity "FTP Yukleme" -Completed
    $runspacePool.Close()
    $runspacePool.Dispose()

    Write-Output "  $completed dosya islendi ($parallelCount paralel)"

    # Basarisiz dosyalar icin retry (3 deneme, 3 sn arayla, paralel)
    if ($failedFiles.Count -gt 0) {
        Write-Output ""
        Write-Output "  $($failedFiles.Count) dosya icin yeniden deneniyor..."
        $retryFiles = $failedFiles
        $failedFiles = @()

        for ($attempt = 1; $attempt -le 3; $attempt++) {
            if ($retryFiles.Count -eq 0) { break }
            Write-Output "  Retry $attempt/3 - $($retryFiles.Count) dosya, 3 sn bekleniyor..."
            Start-Sleep -Seconds 3

            $retryPool = [runspacefactory]::CreateRunspacePool(1, 3)
            $retryPool.Open()
            $retryJobs = @()

            foreach ($rf in $retryFiles) {
                $remoteFilePath = $remotePath.TrimEnd("/") + $rf.Path
                $ps = [powershell]::Create().AddScript($uploadScript)
                $ps.AddArgument($server).AddArgument($port).AddArgument($username).AddArgument($password) | Out-Null
                $ps.AddArgument($useSsl).AddArgument($remoteFilePath).AddArgument($rf.File.FullName).AddArgument($rf.Path) | Out-Null
                $ps.RunspacePool = $retryPool
                $retryJobs += @{ PS = $ps; Handle = $ps.BeginInvoke(); OrigItem = $rf }
            }

            $stillFailed = @()
            foreach ($rj in $retryJobs) {
                $result = $rj.PS.EndInvoke($rj.Handle)
                $rj.PS.Dispose()
                if ($result -and $result.Count -gt 0 -and $result[0].OK) {
                    Write-Output "  OK: $($rj.OrigItem.Path) basariyla yuklendi"
                } else {
                    $stillFailed += $rj.OrigItem
                }
            }
            $retryPool.Close()
            $retryPool.Dispose()
            $retryFiles = $stillFailed
        }

        $failedFiles = $retryFiles
    }

    # Silinen dosyalari sunucudan kaldir
    foreach ($delFile in $deletedFiles) {
        $remoteFilePath = $remotePath.TrimEnd("/") + $delFile
        try {
            $ftpUri = "ftp://${server}:${port}${remoteFilePath}"
            $ftpRequest = [System.Net.FtpWebRequest]::Create($ftpUri)
            $ftpRequest.Method = [System.Net.WebRequestMethods+Ftp]::DeleteFile
            $ftpRequest.Credentials = New-Object System.Net.NetworkCredential($username, $password)
            $ftpRequest.EnableSsl = $useSsl
            $ftpRequest.UsePassive = $true
            $resp = $ftpRequest.GetResponse()
            $resp.Close()
            Write-Output "  Silindi: $delFile"
        } catch {
            Write-Output "  UYARI: $delFile silinemedi: $($_.Exception.Message)"
        }
    }

    if ($failedFiles.Count -gt 0) {
        Write-Output ""
        Write-Output "  $($failedFiles.Count) dosya yuklenemedi:"
        foreach ($f in $failedFiles) {
            Write-Output "    - $($f.Path): $($f.Error)"
        }
    }

    # --- app_offline.htm sil (uygulamayi yeniden baslatir) ---
    Write-Output "  app_offline.htm siliniyor (uygulama yeniden baslatiliyor)..."
    try {
        $offlineDelUri = "ftp://${server}:${port}$($remotePath.TrimEnd('/'))/app_offline.htm"
        $offlineDelReq = [System.Net.FtpWebRequest]::Create($offlineDelUri)
        $offlineDelReq.Method = [System.Net.WebRequestMethods+Ftp]::DeleteFile
        $offlineDelReq.Credentials = New-Object System.Net.NetworkCredential($username, $password)
        $offlineDelReq.EnableSsl = $useSsl
        $offlineDelReq.UsePassive = $true
        $offlineDelResp = $offlineDelReq.GetResponse()
        $offlineDelResp.Close()
        Write-Output "  app_offline.htm silindi, uygulama yeniden basladi!"
    } catch {
        Write-Output "  UYARI: app_offline.htm silinemedi: $($_.Exception.Message)"
        Write-Output "  Sunucudan manuel olarak silin: $($remotePath.TrimEnd('/'))/app_offline.htm"
        if ($_.Exception.InnerException) {
            Write-Output "  Detay: $($_.Exception.InnerException.Message)"
        }
    }

    $failedPaths = @()
    foreach ($f in $failedFiles) { $failedPaths += $f.Path }
    return @{ FailCount = $failedFiles.Count; FailedPaths = $failedPaths }
}

function Publish-FileCopy {
    param($config, $publishDir)

    $destPath = $config.FileCopy.DestinationPath


    if ([string]::IsNullOrWhiteSpace($destPath) -or $destPath -eq "\\sunucu\paylasim\site\") {
        Write-Output "HATA: Lütfen deploy-config.json içindeki FileCopy hedef yolunu güncelleyin!"
        exit 1
    }

    Write-Output "  Hedef: $destPath"

    if (-not (Test-Path $destPath)) {
        Write-Output "  Hedef klasör oluşturuluyor..."
        New-Item -ItemType Directory -Path $destPath -Force | Out-Null
    }

    # Mevcut dosyaları yedekle
    $backupDir = "${destPath}_backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    if ((Get-ChildItem $destPath -ErrorAction SilentlyContinue | Measure-Object).Count -gt 0) {
        Write-Output "  Mevcut dosyalar yedekleniyor: $backupDir"
        Copy-Item -Path $destPath -Destination $backupDir -Recurse -Force
    }

    # Dosyaları kopyala
    $files = Get-ChildItem $publishDir -Recurse -File
    $totalFiles = $files.Count
    $currentFile = 0

    foreach ($file in $files) {
        $currentFile++
        $relativePath = $file.FullName.Substring($publishDir.Length)
        $targetPath = Join-Path $destPath $relativePath
        $targetDir = Split-Path $targetPath -Parent

        if (-not (Test-Path $targetDir)) {
            New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        }

        $percent = [math]::Round(($currentFile / $totalFiles) * 100)
        Write-Progress -Activity "Dosya Kopyalama" -Status "$currentFile / $totalFiles - $relativePath" -PercentComplete $percent

        Copy-Item -Path $file.FullName -Destination $targetPath -Force
    }

    Write-Progress -Activity "Dosya Kopyalama" -Completed
    return 0
}

# --- Secilen Yonteme Gore Dagitim ---
$failCount = 0
$failedPaths = @()

switch ($deployMethod) {
    "ftp" {
        $result = Publish-FTP -config $config -publishDir $publishDir -changedFiles $changedFiles -deletedFiles $deletedFiles
        $failCount = $result.FailCount
        $failedPaths = if ($result.FailedPaths) { $result.FailedPaths } else { @() }
    }
    "filecopy" {
        $failCount = Publish-FileCopy -config $config -publishDir $publishDir
    }
    default {
        Write-Output "HATA: Bilinmeyen dağıtım yöntemi: $deployMethod"
        Write-Output "Desteklenen yöntemler: ftp, filecopy"
        exit 1
    }
}

Write-Output "  Yukleme suresi: $($stepWatch.Elapsed.TotalSeconds.ToString('0.0'))s"
Write-Output ""

# --- 4. Manifest Kaydet ---
Write-Output "[4/4] Manifest guncelleniyor..."

# Basarisiz dosyalarin hash'ini eski haline dondur (sonraki deploy'da tekrar denensin)
foreach ($fp in $failedPaths) {
    if ($previousManifest.ContainsKey($fp)) {
        $currentManifest[$fp] = $previousManifest[$fp]
    } else {
        $currentManifest.Remove($fp)
    }
}

$currentManifest | ConvertTo-Json | Set-Content $manifestPath -Encoding UTF8
$successCount = $changedFiles.Count - $failCount
Write-Output "[4/4] Manifest kaydedildi ($successCount basarili dosya islendi)"

# --- Sonuc ---
Write-Output ""
Write-Output "============================================="
if ($failCount -eq 0) {
    Write-Output "   DAGITIM BASARILI!"
    Write-Output "   Yuklenen: $($changedFiles.Count) dosya"
    if ($newFiles.Count -gt 0) {
        Write-Output "   Yeni: $($newFiles.Count) dosya"
    }
    if ($modifiedFiles.Count -gt 0) {
        Write-Output "   Guncellenen: $($modifiedFiles.Count) dosya"
    }
    if ($deletedFiles.Count -gt 0) {
        Write-Output "   Silinen: $($deletedFiles.Count) dosya"
    }
} else {
    Write-Output "   DAGITIM TAMAMLANDI ($failCount hata ile)"
    Write-Output "   Basarili: $($changedFiles.Count - $failCount) dosya"
    Write-Output "   Basarisiz: $failCount dosya (sonraki deploy'da tekrar denenecek)"
}
$totalStopwatch.Stop()
Write-Output "   Tarih: $(Get-Date -Format 'dd.MM.yyyy HH:mm:ss')"
Write-Output "   Toplam sure: $($totalStopwatch.Elapsed.TotalSeconds.ToString('0.0')) saniye"
Write-Output "============================================="
