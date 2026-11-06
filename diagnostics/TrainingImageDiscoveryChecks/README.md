# 訓練圖庫辨識檢查

.NET 10 獨立檢查，連結正式 TrainingImageDiscovery，不啟動主程式或寫入配方。測試 fixture 為專用 artifacts 目錄內的空白假圖片檔，只檢查檔名／目錄辨識，不做影像解碼。

```powershell
$build = Join-Path $env:TEMP ('LumaTrainingChecks-' + [Guid]::NewGuid().ToString('N'))
dotnet run --project .\diagnostics\TrainingImageDiscoveryChecks\TrainingImageDiscoveryChecks.csproj -c Release --artifacts-path $build
```

可額外傳入 20160H 測試 TrainingImage 路徑，唯讀確認第 1～6 站與實際圖片數量。支援 root 前綴推斷、CCD01／CCD1、CCD10、實際內層站號、同 CCD 多站、拼字／補零、版本不合併與多產品歧義檢查。
