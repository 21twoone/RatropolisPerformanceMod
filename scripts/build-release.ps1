param(
    [string]$GameRoot = $env:RATROPOLIS_DIR
)

$ErrorActionPreference = 'Stop'

$version = '1.1.0'
$bepInExVersion = '5.4.23.4'
$projectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$artifactRoot = Join-Path $projectRoot 'artifacts'
$stagingRoot = Join-Path $artifactRoot 'staging'
$fullStage = Join-Path $stagingRoot 'full'
$pluginStage = Join-Path $stagingRoot 'plugin-only'
$archivePath = Join-Path $projectRoot "deps\BepInEx_win_x86_$bepInExVersion.zip"
$bepInExDirectory = Join-Path $projectRoot "deps\BepInEx_win_x86_$bepInExVersion\BepInEx"
$pluginPath = Join-Path $projectRoot 'bin\Release\RatropolisPerformanceMod.dll'

if (-not $GameRoot) {
    throw 'Set RATROPOLIS_DIR or pass -GameRoot "C:\path\to\Ratropolis".'
}

$GameRoot = [System.IO.Path]::GetFullPath($GameRoot)
if (-not (Test-Path -LiteralPath (Join-Path $GameRoot 'Ratropolis.exe'))) {
    throw "Ratropolis.exe was not found under: $GameRoot"
}

if (-not (Test-Path -LiteralPath $archivePath)) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $archivePath) |
        Out-Null
    $downloadUrl = "https://github.com/BepInEx/BepInEx/releases/download/v$bepInExVersion/BepInEx_win_x86_$bepInExVersion.zip"
    Invoke-WebRequest -Uri $downloadUrl -OutFile $archivePath
}

& dotnet build `
    (Join-Path $projectRoot 'RatropolisPerformanceMod.csproj') `
    -c Release `
    "-p:RatropolisDir=$GameRoot" `
    "-p:BepInExDir=$bepInExDirectory"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $pluginPath)) {
    throw "Built plugin not found: $pluginPath"
}

$resolvedProjectRoot = [System.IO.Path]::GetFullPath($projectRoot)
$resolvedStagingRoot = [System.IO.Path]::GetFullPath($stagingRoot)
if (-not $resolvedStagingRoot.StartsWith(
    $resolvedProjectRoot,
    [System.StringComparison]::OrdinalIgnoreCase
)) {
    throw "Refusing to clean staging outside the project: $resolvedStagingRoot"
}

if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $fullStage, $pluginStage | Out-Null
Expand-Archive -LiteralPath $archivePath -DestinationPath $fullStage -Force

$fullPluginDirectory = Join-Path $fullStage 'BepInEx\plugins'
$pluginOnlyDirectory = Join-Path $pluginStage 'BepInEx\plugins'
New-Item -ItemType Directory -Force -Path $fullPluginDirectory, $pluginOnlyDirectory |
    Out-Null

Copy-Item -LiteralPath $pluginPath -Destination $fullPluginDirectory -Force
Copy-Item -LiteralPath $pluginPath -Destination $pluginOnlyDirectory -Force

foreach ($fileName in @(
    'README.md',
    'LICENSE',
    'THIRD_PARTY_NOTICES.md',
    'install.ps1',
    'uninstall-mod.ps1'
)) {
    Copy-Item `
        -LiteralPath (Join-Path $projectRoot $fileName) `
        -Destination $fullStage `
        -Force
}

Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $pluginStage
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $pluginStage

$bepInExLicenseUrl = "https://raw.githubusercontent.com/BepInEx/BepInEx/v$bepInExVersion/LICENSE"
Invoke-WebRequest `
    -Uri $bepInExLicenseUrl `
    -OutFile (Join-Path $fullStage 'BEPINEX-LICENSE.txt')

$fullZip = Join-Path $artifactRoot "RatropolisPerformanceMod-v$version-win-x86.zip"
$pluginZip = Join-Path $artifactRoot "RatropolisPerformanceMod-v$version-plugin-only.zip"
foreach ($zipPath in @($fullZip, $pluginZip)) {
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
}

Compress-Archive -Path (Join-Path $fullStage '*') -DestinationPath $fullZip
Compress-Archive -Path (Join-Path $pluginStage '*') -DestinationPath $pluginZip

Write-Host "Created $fullZip"
Write-Host "Created $pluginZip"
