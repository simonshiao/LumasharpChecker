$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Source\LumasharpChecker\LumasharpChecker.csproj'
$app = Join-Path $PSScriptRoot 'App'
dotnet publish $project -c Release -p:Platform=x64 -p:SlimPublish=true -o $app --nologo
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
