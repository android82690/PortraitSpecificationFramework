# Build the Portrait Specification Framework and install it into the Elin Package folder.
#
# Game location: -ElinDir / -WorkshopDir, else the ELIN_DIR / ELIN_WORKSHOP_DIR
# environment variables, else the default Steam install.
#
# package.xml in the GAME folder is USER-OWNED: the install never overwrites
# it, it only patches the <version> line in place. It is only created fresh
# from the repo copy when missing entirely.
param(
    [string]$ElinDir = "",
    [string]$WorkshopDir = "",
    [switch]$NoInstall
)
$ErrorActionPreference = "Stop"
if (-not $ElinDir) { $ElinDir = $env:ELIN_DIR }
if (-not $ElinDir) { $ElinDir = "C:\Program Files (x86)\Steam\steamapps\common\Elin" }
if (-not $WorkshopDir) { $WorkshopDir = $env:ELIN_WORKSHOP_DIR }
if (-not $WorkshopDir) { $WorkshopDir = Join-Path $ElinDir "..\..\workshop\content\2135150" }
$root = $PSScriptRoot
$dotnet = Join-Path $HOME ".dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }

& $dotnet build "$root\SpecificPortraits.csproj" -c Release -p:ElinDir=$ElinDir -p:WorkshopDir=$WorkshopDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $NoInstall) {
    $dest = Join-Path $ElinDir "Package\Mod_PortraitSpecificationFramework"
    New-Item -ItemType Directory -Force $dest | Out-Null
    Copy-Item "$root\bin\Release\SpecificPortraits.dll" $dest -Force
    Copy-Item "$root\package\*" $dest -Recurse -Force -Exclude "package.xml"
    $destMeta = Join-Path $dest "package.xml"
    if (Test-Path $destMeta) {
        $repoMeta = Get-Content "$root\package\package.xml" -Raw
        if ($repoMeta -match '<version>([0-9.]+)</version>') {
            $v = $Matches[1]
            $meta = Get-Content $destMeta -Raw
            $patched = $meta -replace '<version>[0-9.]+</version>', "<version>$v</version>"
            if ($patched -ne $meta) { Set-Content $destMeta -Value $patched -NoNewline }
        }
    }
    else {
        Copy-Item "$root\package\package.xml" $destMeta
    }
    Write-Host "Installed to $dest"
}
