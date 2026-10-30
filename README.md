# LumasharpCheckerV2

以 1.0.0.69 為基底的非 AI 開發版本，目前版本 2.0.0.1，使用 WPF、.NET Framework 4.7.2 與 Windows x64。

- 保留非 AI 定位、光源與銳利度檢測的開發程式碼。
- 定位與原廠 R3 工具共用 App/NativeR3，避免重複發布原生 DLL。
- 不部署 CUDA、TensorRT、Python 訓練環境或 AI Runtime；AI 定位 Recipe 會明確拒絕執行。
- 本倉庫只包含原始碼、腳本與文件。本機可攜 App、原廠二進位元件、Recipe、資料庫、模型與樣本不納入 Git。

## 本機建置與啟動

需要 Windows x64、.NET SDK，以及 .NET Framework 4.7.2 targeting pack（或等效參考組件）。

先準備合法取得的非 AI R3 執行環境到 App/NativeR3。定位專案至少需要其中的 InspTools.dll 與 OpenCvSharp.dll；實際執行還需要 R3 的原生 OpenCV DLL、介面依賴、InspToolsStandalone.exe、InspToolsRecipeCli.exe、LumaNativeFlowHost.exe 及既有 portable-review.marker。原廠元件不由此倉庫下載或重建，因此單獨 clone 並不包含完整執行環境。

執行：

```powershell
powershell -ExecutionPolicy Bypass -File .\建置v2.ps1
```

建置前先關閉 v2。完成後以「啟動v2.vbs」開啟 App/LumasharpChecker.exe。沒有資料庫時 LiteDB 會於使用時建立；Recipe 與圖庫需自行設定。

## 目前驗證範圍

Release x64 發布成功；1.0.0.69 主視窗啟動且可回應；一張 R002 樣本的非 AI 定位成功。尚未完成全部樣本、八站、參數編輯／存檔與實機驗收。

本機完整開發目錄精簡後約 195 MB，倉庫只保存其中的原始碼部分。整理細節見 [README-v2.md](README-v2.md)。
