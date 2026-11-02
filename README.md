# LumasharpChecker v3

v3 以 v2 為基礎，聚焦多站光源點檢。側邊流程只保留定位、選向、選面、光源檢測與結果；停用的灰色工具卡及「檢測與分析」區已移除。

- WPF、.NET Framework 4.7.2、Windows x64。
- 配方、站別設定、圖庫、光源 ROI 與亮度上下限仍由各配方管理。
- 保留連續檢測、結果檢視與 Excel 匯出。
- v3 使用獨立單實例識別及 3.0.0.0 版本號，可與 v2 區分。

## 建置與啟動

先關閉 v3，再執行：

```powershell
powershell -ExecutionPolicy Bypass -File .\建置v3.ps1
```

完成後以 `啟動v3.vbs` 開啟 `App/LumasharpChecker.exe`。執行環境需有本機 `App/NativeR3` 原廠元件；配方、圖庫及站別設定保存在 App 目錄。

## v2 基底

`README-v2.md` 保留前一版的開發與驗證紀錄。v3 是獨立資料夾副本，v2 專案不受本版介面調整影響。
