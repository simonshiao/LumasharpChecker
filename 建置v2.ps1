$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Source\LumasharpChecker\LumasharpChecker.csproj'
$app = Join-Path $PSScriptRoot 'App'
$flow = Join-Path $PSScriptRoot 'Source\LumaNativeFlowHost\LumaNativeFlowHost.csproj'
dotnet build $flow -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Native flow build failed.' }
dotnet publish $project -c Release -p:Platform=x64 -p:SlimPublish=true -o $app --nologo
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
$nativeRoot = Join-Path $app 'NativeR3'
if (-not (Test-Path -LiteralPath $nativeRoot -PathType Container)) { throw 'Missing App/NativeR3 runtime.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Source\LumaNativeFlowHost\bin\Release\net472\LumaNativeFlowHost.exe') -Destination $nativeRoot -Force
