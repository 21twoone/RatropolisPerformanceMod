param(
    [string]$GameRoot
)

$ErrorActionPreference = 'Stop'

function Find-Ratropolis {
    param([string]$RequestedRoot)

    if ($RequestedRoot) {
        $candidate = [System.IO.Path]::GetFullPath($RequestedRoot)
        if (Test-Path -LiteralPath (Join-Path $candidate 'Ratropolis.exe')) {
            return $candidate
        }

        throw "Ratropolis.exe was not found under: $candidate"
    }

    $steamRoots = New-Object 'System.Collections.Generic.List[string]'
    foreach ($registryPath in @(
        'HKCU:\Software\Valve\Steam',
        'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam',
        'HKLM:\SOFTWARE\Valve\Steam'
    )) {
        $steam = Get-ItemProperty -Path $registryPath -ErrorAction SilentlyContinue
        foreach ($propertyName in @('SteamPath', 'InstallPath')) {
            $value = $steam.$propertyName
            if ($value -and -not $steamRoots.Contains($value)) {
                $steamRoots.Add($value)
            }
        }
    }

    foreach ($steamRoot in $steamRoots) {
        $vdfPath = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
        $libraries = @($steamRoot)
        if (Test-Path -LiteralPath $vdfPath) {
            $vdf = Get-Content -LiteralPath $vdfPath -Raw
            $libraries += [regex]::Matches($vdf, '"path"\s+"([^"]+)"') |
                ForEach-Object { $_.Groups[1].Value.Replace('\\', '\') }
        }

        foreach ($library in $libraries) {
            $candidate = Join-Path $library 'steamapps\common\Ratropolis'
            if (Test-Path -LiteralPath (Join-Path $candidate 'Ratropolis.exe')) {
                return [System.IO.Path]::GetFullPath($candidate)
            }
        }
    }

    throw 'Ratropolis was not found. Run again with -GameRoot "C:\path\to\Ratropolis".'
}

if (Get-Process -Name 'Ratropolis' -ErrorAction SilentlyContinue) {
    throw 'Ratropolis is running. Close the game before uninstalling.'
}

$resolvedGameRoot = Find-Ratropolis -RequestedRoot $GameRoot
$pluginPath = Join-Path $resolvedGameRoot 'BepInEx\plugins\RatropolisPerformanceMod.dll'
$configPath = Join-Path $resolvedGameRoot 'BepInEx\config\local.ratropolis.performance.cfg'

foreach ($target in @($pluginPath, $configPath)) {
    if (-not (Test-Path -LiteralPath $target)) {
        continue
    }

    $resolvedTarget = (Resolve-Path -LiteralPath $target).Path
    if (-not $resolvedTarget.StartsWith(
        $resolvedGameRoot,
        [System.StringComparison]::OrdinalIgnoreCase
    )) {
        throw "Refusing to remove a path outside the game directory: $resolvedTarget"
    }

    Remove-Item -LiteralPath $resolvedTarget -Force
    Write-Host "Removed $resolvedTarget"
}
