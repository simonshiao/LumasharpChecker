# 光源樣本與配方同步驗證

使用 .NET 10 執行獨立檔案儲存檢查，不啟動原生檢測、不讀寫正式配方。

```powershell
$build = Join-Path $env:TEMP ('LumaStorageChecks-' + [Guid]::NewGuid().ToString('N'))
dotnet run --project .\diagnostics\LightSampleStorageChecks\LightSampleStorageChecks.csproj -c Release --artifacts-path $build
```

Fixture 只寫入測試執行檔所在的 artifacts 目錄。完成後可清除該次專用暫存目錄。
檢查原圖與參數往返、內容去重、最近三份保留、不同配方／站別拒絕、毀損圖片校驗、交易失敗回復、原生配方變更同步與交易副本清除。
