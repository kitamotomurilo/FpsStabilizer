<#
.SYNOPSIS
    Build script for FPS Stabilizer
.DESCRIPTION
    Compila a solution em modo Release e copia executáveis para pasta de distribuição
#>

param(
    [switch]$Clean,
    [switch]$RunTests,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$SolutionPath = "FpsStabilizer.sln"
$ProjectRoot = $PSScriptRoot

Write-Host "=== FPS Stabilizer Build Script ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow

# Verifica .NET SDK
try {
    $dotnetVersion = dotnet --version
    Write-Host ".NET SDK: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Error ".NET SDK não encontrado. Instale: winget install Microsoft.DotNet.SDK.8"
    exit 1
}

if ($Clean) {
    Write-Host "`n[1/4] Limpando builds anteriores..." -ForegroundColor Cyan
    dotnet clean $SolutionPath --configuration $Configuration
    Remove-Item -Recurse -Force "$ProjectRoot\src\*\bin" -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force "$ProjectRoot\src\*\obj" -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force "$ProjectRoot\artifacts" -ErrorAction SilentlyContinue
}

Write-Host "`n[2/4] Restaurando pacotes NuGet..." -ForegroundColor Cyan
dotnet restore $SolutionPath

Write-Host "`n[3/4] Compilando..." -ForegroundColor Cyan
dotnet build $SolutionPath --configuration $Configuration --no-restore

if ($RunTests) {
    Write-Host "`n[4/4] Executando testes..." -ForegroundColor Cyan
    dotnet test $SolutionPath --configuration $Configuration --no-build --verbosity normal
} else {
    Write-Host "`n[4/4] Pulando testes (use -RunTests para executar)" -ForegroundColor Yellow
}

# Copia executáveis para pasta de distribuição
$DistPath = "$ProjectRoot\artifacts\dist"
$UiExe = "$ProjectRoot\src\FpsStabilizer.UI\bin\$Configuration\net8.0-windows\FpsStabilizer.exe"
$ServiceExe = "$ProjectRoot\src\FpsStabilizer.Service\bin\$Configuration\net8.0-windows\FpsStabilizer.Service.exe"

if ((Test-Path $UiExe) -or (Test-Path $ServiceExe)) {
    Write-Host "`n[5/5] Criando pasta de distribuição..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path $DistPath | Out-Null

    if (Test-Path $UiExe) {
        Copy-Item $UiExe -Destination "$DistPath\FpsStabilizer.exe" -Force
        Copy-Item "$ProjectRoot\src\FpsStabilizer.UI\bin\$Configuration\net8.0-windows\FpsStabilizer.dll" -Destination "$DistPath\" -Force -ErrorAction SilentlyContinue
        Write-Host "  ✓ FpsStabilizer.exe copiado" -ForegroundColor Green
    }

    if (Test-Path $ServiceExe) {
        Copy-Item $ServiceExe -Destination "$DistPath\FpsStabilizer.Service.exe" -Force
        Write-Host "  ✓ FpsStabilizer.Service.exe copiado" -ForegroundColor Green
    }

    # Copia dependências necessárias
    $Deps = @(
        "CommunityToolkit.Mvvm.dll",
        "Hardcodet.NotifyIcon.Wpf.dll",
        "Newtonsoft.Json.dll",
        "System.Management.dll"
    )

    foreach ($dep in $Deps) {
        $src = "$ProjectRoot\src\FpsStabilizer.UI\bin\$Configuration\net8.0-windows\$dep"
        if (Test-Path $src) {
            Copy-Item $src -Destination "$DistPath\$dep" -Force -ErrorAction SilentlyContinue
        }
    }

    Write-Host "`nDistribuição pronta em: $DistPath" -ForegroundColor Green
}

Write-Host "`n=== Build concluído com sucesso! ===" -ForegroundColor Cyan