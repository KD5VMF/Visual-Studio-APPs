$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$project = Join-Path $PSScriptRoot 'src\HelixSolarShow\HelixSolarShow.csproj'
dotnet publish $project -c Release -r win-x64 --self-contained false /p:PublishSingleFile=false
