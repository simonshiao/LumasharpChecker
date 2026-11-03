# 標準件亮度量測驗證

測試共享 HSV V 量測與初始 ROI，不讀寫正式配方。使用 .NET Framework 4.7.2 x64，需 App 的 cvextern.dll 原生元件。

在專案根目錄執行：

```powershell
$env:PATH = (Join-Path $PWD 'App') + ';' + $env:PATH
$build = Join-Path $env:TEMP ('LumaBrightnessChecks-' + [Guid]::NewGuid().ToString('N'))
dotnet run --project .\diagnostics\LightBrightnessChecks\LightBrightnessChecks.csproj -c Release --artifacts-path $build
```

測試圖片與建置產物只存入本次 artifacts 目錄，驗證後可清除此專用目錄。
