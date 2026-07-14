param(
    [ValidateSet(
        "help",
        "clean",
        "build",
        "rebuild",
        "run",
        "run-nobuild",
        "watch",
        "publish",
        "status",
        "port-check",
        "free-port",
        "stop",
        "full"
    )]
    [string]$Action = "help",

    [int]$Port = 5010,
    [string]$Configuration = "Debug",
    [string]$LaunchProfile = "",
    [string]$Project = "gamabelmvc.csproj",
    [string]$Runtime = "",
    [switch]$SelfContained,
    [switch]$Force
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

function Write-Err {
    param([string]$Message)
    Write-Host "[ERR]  $Message" -ForegroundColor Red
}

function Get-ProjectRoot {
    if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        return $PSScriptRoot
    }

    return (Get-Location).Path
}

function Enter-ProjectRoot {
    $root = Get-ProjectRoot
    Set-Location $root
    Write-Info "Project root: $root"
}

function Invoke-Dotnet {
    param(
        [string[]]$DotnetArguments,
        [string]$Label
    )

    Write-Info "$Label"
    Write-Host ("dotnet " + ($DotnetArguments -join " ")) -ForegroundColor DarkGray

    & dotnet @DotnetArguments

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed (exit code: $LASTEXITCODE)"
    }
}

function Get-PortListeners {
    param([int]$LocalPort)

    $connections = @(Get-NetTCPConnection -State Listen -LocalPort $LocalPort -ErrorAction SilentlyContinue)
    if ($connections.Count -eq 0) {
        return @()
    }

    $result = @()
    foreach ($c in $connections) {
        $proc = Get-Process -Id $c.OwningProcess -ErrorAction SilentlyContinue
        $result += [PSCustomObject]@{
            Port    = $LocalPort
            PID     = $c.OwningProcess
            Process = if ($proc) { $proc.ProcessName } else { "Unknown" }
        }
    }

    return $result
}

function Stop-PortListeners {
    param([int]$LocalPort)

    $listeners = Get-PortListeners -LocalPort $LocalPort
    if ($listeners.Count -eq 0) {
        Write-Ok "Port $LocalPort bos."
        return
    }

    foreach ($item in $listeners) {
        try {
            Stop-Process -Id $item.PID -Force -ErrorAction Stop
            Write-Ok "PID $($item.PID) ($($item.Process)) durduruldu."
        }
        catch {
            Write-Warn "PID $($item.PID) durdurulamadi: $($_.Exception.Message)"
        }
    }
}

function Show-Usage {
    Write-Host ""
    Write-Host "Gamabel MVC Dev Tools (PowerShell)" -ForegroundColor White
    Write-Host "----------------------------------" -ForegroundColor White
    Write-Host ""
    Write-Host "Ornek kullanim:" -ForegroundColor Cyan
    Write-Host "  .\dev-tools.ps1 -Action build"
    Write-Host "  .\dev-tools.ps1 -Action run -Port 5099"
    Write-Host "  .\dev-tools.ps1 -Action run-nobuild -Port 5099"
    Write-Host "  .\dev-tools.ps1 -Action watch -Port 5099"
    Write-Host "  .\dev-tools.ps1 -Action port-check -Port 5099"
    Write-Host "  .\dev-tools.ps1 -Action free-port -Port 5099"
    Write-Host "  .\dev-tools.ps1 -Action publish -Configuration Release"
    Write-Host "  .\dev-tools.ps1 -Action full -Port 5010"
    Write-Host ""
    Write-Host "Action listesi:" -ForegroundColor Cyan
    Write-Host "  help       : Bu yardim"
    Write-Host "  clean      : bin ve obj temizligi"
    Write-Host "  build      : dotnet restore + build"
    Write-Host "  rebuild    : clean + build"
    Write-Host "  run        : build ederek uygulamayi secilen portta calistirir"
    Write-Host "  run-nobuild: build etmeden calistirir"
    Write-Host "  watch      : dotnet watch run"
    Write-Host "  publish    : publish cikisi alir"
    Write-Host "  status     : calisan dotnet/gamabelmvc processlerini gosterir"
    Write-Host "  port-check : portu dinleyen process var mi kontrol eder"
    Write-Host "  free-port  : portu kullanan processleri durdurur"
    Write-Host "  stop       : gamabelmvc ve dotnet processlerini durdurur"
    Write-Host "  full       : port bos -> clean -> rebuild -> run (KOMPLE SUREC)"
    Write-Host ""
    Write-Host "Notlar:" -ForegroundColor Cyan
    Write-Host "  - Port default: 5010"
    Write-Host "  - Profile verirseniz: dotnet run --launch-profile <Profile>"
    Write-Host "  - Profile vermesseniz: dotnet run --urls http://localhost:<Port>"
    Write-Host ""
}

try {
    Enter-ProjectRoot

    switch ($Action) {
        "help" {
            Show-Usage
        }

        "clean" {
            Write-Info "bin ve obj klasorleri temizleniyor..."
            Remove-Item -Recurse -Force "bin" -ErrorAction SilentlyContinue
            Remove-Item -Recurse -Force "obj" -ErrorAction SilentlyContinue
            Write-Ok "Temizlik tamamlandi."
        }

        "build" {
            if ($Force) {
                Write-Info "--force aktif: restore zorlanacak"
                Invoke-Dotnet -DotnetArguments @("restore", "--force") -Label "NuGet restore"
            }
            else {
                Invoke-Dotnet -DotnetArguments @("restore") -Label "NuGet restore"
            }

            Invoke-Dotnet -DotnetArguments @("build", "-c", $Configuration) -Label "Build"
            Write-Ok "Build tamamlandi."
        }

        "rebuild" {
            Write-Info "Rebuild basliyor (clean + build)..."
            Remove-Item -Recurse -Force "bin" -ErrorAction SilentlyContinue
            Remove-Item -Recurse -Force "obj" -ErrorAction SilentlyContinue

            if ($Force) {
                Invoke-Dotnet -DotnetArguments @("restore", "--force") -Label "NuGet restore"
            }
            else {
                Invoke-Dotnet -DotnetArguments @("restore") -Label "NuGet restore"
            }

            Invoke-Dotnet -DotnetArguments @("build", "-c", $Configuration) -Label "Build"
            Write-Ok "Rebuild tamamlandi."
        }

        "run" {
            $listeners = Get-PortListeners -LocalPort $Port
            if ($listeners.Count -gt 0) {
                Write-Warn "Port $Port zaten kullanimda."
                $listeners | Format-Table -AutoSize
                throw "Port dolu. Once free-port kullanin veya farkli port secin."
            }

            if (-not [string]::IsNullOrWhiteSpace($LaunchProfile)) {
                Invoke-Dotnet -DotnetArguments @("run", "--project", $Project, "--launch-profile", $LaunchProfile) -Label "Uygulama baslatiliyor (profile)"
            }
            else {
                Invoke-Dotnet -DotnetArguments @("run", "--project", $Project, "--urls", "http://localhost:$Port") -Label "Uygulama baslatiliyor"
            }
        }

        "run-nobuild" {
            $listeners = Get-PortListeners -LocalPort $Port
            if ($listeners.Count -gt 0) {
                Write-Warn "Port $Port zaten kullanimda."
                $listeners | Format-Table -AutoSize
                throw "Port dolu. Once free-port kullanin veya farkli port secin."
            }

            if (-not [string]::IsNullOrWhiteSpace($LaunchProfile)) {
                Invoke-Dotnet -DotnetArguments @("run", "--no-build", "--project", $Project, "--launch-profile", $LaunchProfile) -Label "Uygulama baslatiliyor (no-build, profile)"
            }
            else {
                Invoke-Dotnet -DotnetArguments @("run", "--no-build", "--project", $Project, "--urls", "http://localhost:$Port") -Label "Uygulama baslatiliyor (no-build)"
            }
        }

        "watch" {
            $listeners = Get-PortListeners -LocalPort $Port
            if ($listeners.Count -gt 0) {
                Write-Warn "Port $Port zaten kullanimda."
                $listeners | Format-Table -AutoSize
                throw "Port dolu. Once free-port kullanin veya farkli port secin."
            }

            if (-not [string]::IsNullOrWhiteSpace($LaunchProfile)) {
                Invoke-Dotnet -DotnetArguments @("watch", "--project", $Project, "run", "--launch-profile", $LaunchProfile) -Label "Watch mode baslatiliyor (profile)"
            }
            else {
                Invoke-Dotnet -DotnetArguments @("watch", "--project", $Project, "run", "--urls", "http://localhost:$Port") -Label "Watch mode baslatiliyor"
            }
        }

        "publish" {
            $publishOptions = @("publish", $Project, "-c", $Configuration, "-o", "publish")

            if (-not [string]::IsNullOrWhiteSpace($Runtime)) {
                $publishOptions += @("-r", $Runtime)
            }

            if ($SelfContained) {
                $publishOptions += "--self-contained"
            }
            else {
                $publishOptions += "--no-self-contained"
            }

            Invoke-Dotnet -DotnetArguments $publishOptions -Label "Publish"
            Write-Ok "Publish tamamlandi: ./publish"
        }

        "status" {
            Write-Info "Calisan processler (dotnet/gamabelmvc):"
            $procs = @(Get-Process dotnet, gamabelmvc -ErrorAction SilentlyContinue)
            if ($procs.Count -eq 0) {
                Write-Ok "Calisan process bulunamadi."
            }
            else {
                $procs | Select-Object Id, ProcessName, StartTime | Sort-Object ProcessName, Id | Format-Table -AutoSize
            }

            Write-Host ""
            Write-Info "Dinlenen secili port: $Port"
            $listeners = Get-PortListeners -LocalPort $Port
            if ($listeners.Count -eq 0) {
                Write-Ok "Port $Port bos."
            }
            else {
                $listeners | Format-Table -AutoSize
            }
        }

        "port-check" {
            $listeners = Get-PortListeners -LocalPort $Port
            if ($listeners.Count -eq 0) {
                Write-Ok "Port $Port bos."
            }
            else {
                Write-Warn "Port $Port kullanimda:"
                $listeners | Format-Table -AutoSize
            }
        }

        "free-port" {
            Stop-PortListeners -LocalPort $Port
        }

        "stop" {
            Write-Info "gamabelmvc ve dotnet processleri durduruluyor..."
            $procs = @(Get-Process gamabelmvc, dotnet -ErrorAction SilentlyContinue)
            if ($procs.Count -eq 0) {
                Write-Ok "Durdurulacak process bulunamadi."
            }
            else {
                foreach ($p in $procs) {
                    try {
                        Stop-Process -Id $p.Id -Force -ErrorAction Stop
                        Write-Ok "Durduruldu: $($p.ProcessName) (PID: $($p.Id))"
                    }
                    catch {
                        Write-Warn "Durdurulamadi: $($p.ProcessName) (PID: $($p.Id)) - $($_.Exception.Message)"
                    }
                }
            }
        }

        "full" {
            Write-Info "Full cycle basliyor (stop -> clean -> rebuild -> run)..."
            
            # Adim 1: Port'u boşalt (process'leri durdur)
            Write-Info "Adim 1/4: Port $Port boşaltiliyor..."
            Stop-PortListeners -LocalPort $Port
            Start-Sleep -Milliseconds 1000
            
            # Adim 2: Clean
            Write-Info "Adim 2/4: Temizlik yapiliyor..."
            Remove-Item -Recurse -Force "bin" -ErrorAction SilentlyContinue
            Remove-Item -Recurse -Force "obj" -ErrorAction SilentlyContinue
            Write-Ok "Temizlik tamamlandi."
            
            # Adim 3: Rebuild (restore + build)
            Write-Info "Adim 3/4: Rebuild basliyor..."
            if ($Force) {
                Invoke-Dotnet -DotnetArguments @("restore", "--force") -Label "NuGet restore"
            }
            else {
                Invoke-Dotnet -DotnetArguments @("restore") -Label "NuGet restore"
            }
            Invoke-Dotnet -DotnetArguments @("build", "-c", $Configuration) -Label "Build"
            Write-Ok "Rebuild tamamlandi."
            
            # Adim 4: Uygulamayi baslatma
            Write-Info "Adim 4/4: Uygulama baslatiliyor port $Port'te..."
            Start-Sleep -Milliseconds 500
            
            if (-not [string]::IsNullOrWhiteSpace($LaunchProfile)) {
                Invoke-Dotnet -DotnetArguments @("run", "--project", $Project, "--launch-profile", $LaunchProfile) -Label "Uygulama baslatiliyor (profile)"
            }
            else {
                Invoke-Dotnet -DotnetArguments @("run", "--project", $Project, "--urls", "http://localhost:$Port") -Label "Uygulama baslatiliyor"
            }
        }
    }
}
catch {
    Write-Err $_.Exception.Message
    exit 1
}
