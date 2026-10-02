param(
    [string]$Makensis = "D:\Projekte\nsis-3.10\makensis.exe"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$csproj = Join-Path $root "UOTinker.csproj"
$version = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

$publish = Join-Path $root "publish"
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

dotnet publish $csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Push-Location (Join-Path $root "Setup")
try {
    & $Makensis "/DAPP_VERSION=$version" "UOTinker.nsi"
    if ($LASTEXITCODE -ne 0) { throw "makensis failed" }
} finally {
    Pop-Location
}

$setup = Join-Path $root "Setup\UOTinker_Setup_$version.exe"
Get-Item $setup | Select-Object FullName, Length
(Get-FileHash $setup -Algorithm SHA256).Hash
