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

    foreach ($defaultRoot in @(
        "${env:ProgramFiles(x86)}\Steam",
        "$env:ProgramFiles\Steam"
    )) {
        if ($defaultRoot -and -not $steamRoots.Contains($defaultRoot)) {
            $steamRoots.Add($defaultRoot)
        }
    }

    $libraryRoots = New-Object 'System.Collections.Generic.List[string]'
    foreach ($steamRoot in $steamRoots) {
        if (-not (Test-Path -LiteralPath $steamRoot)) {
            continue
        }

        if (-not $libraryRoots.Contains($steamRoot)) {
            $libraryRoots.Add($steamRoot)
        }

        $vdfPath = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
        if (-not (Test-Path -LiteralPath $vdfPath)) {
            continue
        }

        $vdf = Get-Content -LiteralPath $vdfPath -Raw
        foreach ($match in [regex]::Matches($vdf, '"path"\s+"([^"]+)"')) {
            $library = $match.Groups[1].Value.Replace('\\', '\')
            if (-not $libraryRoots.Contains($library)) {
                $libraryRoots.Add($library)
            }
        }
    }

    foreach ($libraryRoot in $libraryRoots) {
        $candidate = Join-Path $libraryRoot 'steamapps\common\Ratropolis'
        if (Test-Path -LiteralPath (Join-Path $candidate 'Ratropolis.exe')) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }

    throw 'Ratropolis was not found. Run again with -GameRoot "C:\path\to\Ratropolis".'
}

if (Get-Process -Name 'Ratropolis' -ErrorAction SilentlyContinue) {
    throw 'Ratropolis is running. Save and close the game before installing.'
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$resolvedGameRoot = Find-Ratropolis -RequestedRoot $GameRoot
$pluginDirectory = Join-Path $resolvedGameRoot 'BepInEx\plugins'

$pluginCandidates = @(
    (Join-Path $scriptRoot 'BepInEx\plugins\RatropolisPerformanceMod.dll'),
    (Join-Path $scriptRoot 'bin\Release\RatropolisPerformanceMod.dll')
)
$pluginSource = $pluginCandidates |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1

if (-not $pluginSource) {
    throw 'RatropolisPerformanceMod.dll was not found in this package. Download a release package or build the project first.'
}

$bepInExSources = @(
    $scriptRoot,
    (Join-Path $scriptRoot 'deps\BepInEx_win_x86_5.4.23.4')
)
$bepInExSource = $bepInExSources |
    Where-Object {
        Test-Path -LiteralPath (Join-Path $_ 'BepInEx\core\BepInEx.dll')
    } |
    Select-Object -First 1

if ($bepInExSource) {
    $resolvedBepInExSource = [System.IO.Path]::GetFullPath($bepInExSource)
    if (-not [string]::Equals(
        $resolvedBepInExSource.TrimEnd('\'),
        $resolvedGameRoot.TrimEnd('\'),
        [System.StringComparison]::OrdinalIgnoreCase
    )) {
        Copy-Item `
            -LiteralPath (Join-Path $bepInExSource 'BepInEx') `
            -Destination $resolvedGameRoot `
            -Recurse `
            -Force

        foreach ($fileName in @(
            '.doorstop_version',
            'changelog.txt',
            'doorstop_config.ini',
            'winhttp.dll'
        )) {
            $sourcePath = Join-Path $bepInExSource $fileName
            if (Test-Path -LiteralPath $sourcePath) {
                Copy-Item `
                    -LiteralPath $sourcePath `
                    -Destination (Join-Path $resolvedGameRoot $fileName) `
                    -Force
            }
        }
    }
}
elseif (-not (Test-Path -LiteralPath (Join-Path $resolvedGameRoot 'BepInEx\core\BepInEx.dll'))) {
    throw 'BepInEx is not installed. Use the full win-x86 release package.'
}

New-Item -ItemType Directory -Force -Path $pluginDirectory | Out-Null
$pluginDestination = Join-Path $pluginDirectory 'RatropolisPerformanceMod.dll'
if (-not [string]::Equals(
    [System.IO.Path]::GetFullPath($pluginSource),
    [System.IO.Path]::GetFullPath($pluginDestination),
    [System.StringComparison]::OrdinalIgnoreCase
)) {
    Copy-Item `
        -LiteralPath $pluginSource `
        -Destination $pluginDestination `
        -Force
}

Write-Host "Installed Ratropolis Performance Mod to $resolvedGameRoot"
