$ErrorActionPreference = "Stop"

dotnet publish .\NextbotImporter\NextbotImporter.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None -p:DebugSymbols=false `
  -o .\dist

Write-Host "Build complete: $PSScriptRoot\dist"
