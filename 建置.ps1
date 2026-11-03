param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'App'))
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Source\LumasharpChecker\LumasharpChecker.csproj'
$flow = Join-Path $PSScriptRoot 'Source\LumaNativeFlowHost\LumaNativeFlowHost.csproj'
$app = [IO.Path]::GetFullPath($OutputDirectory)
$nativeRoot = Join-Path $app 'NativeR3'
if (-not (Test-Path -LiteralPath $nativeRoot -PathType Container)) { throw 'Missing NativeR3 runtime in output folder.' }
$appExe = Join-Path $app 'LumasharpChecker.exe'
$running = Get-Process LumasharpChecker -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $appExe }
if ($running) { throw 'Please save and close LumasharpChecker before building.' }
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
$artifacts = Join-Path $tempRoot ('LumasharpBuild-' + [Guid]::NewGuid().ToString('N'))
try {
    dotnet build $flow -c Release --artifacts-path $artifacts --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Native flow build failed.' }
    dotnet publish $project -c Release -p:Platform=x64 -p:SlimPublish=true --artifacts-path $artifacts -o $app --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    foreach ($hostName in @('LumaNativeFlowHost', 'InspToolsLocator')) {
        $hostExe = @(Get-ChildItem -LiteralPath (Join-Path $artifacts ('bin\' + $hostName)) -Filter ($hostName + '.exe') -Recurse -File)
        if ($hostExe.Count -ne 1) { throw "Expected exactly one $hostName executable." }
        Copy-Item -LiteralPath $hostExe[0].FullName -Destination $nativeRoot -Force
        $config = $hostExe[0].FullName + '.config'
        if (Test-Path -LiteralPath $config) { Copy-Item -LiteralPath $config -Destination $nativeRoot -Force }
    }
}
finally {
    # Delete only the unique build directory created above, outside Source and App.
    $resolvedArtifacts = [IO.Path]::GetFullPath($artifacts)
    if ([IO.Path]::GetDirectoryName($resolvedArtifacts) -ne $tempRoot -or [IO.Path]::GetFileName($resolvedArtifacts) -notmatch '^LumasharpBuild-[a-f0-9]{32}$') {
        throw 'Unexpected build cache path; cleanup refused.'
    }
    if (Test-Path -LiteralPath $resolvedArtifacts) { Remove-Item -LiteralPath $resolvedArtifacts -Recurse -Force }
}
