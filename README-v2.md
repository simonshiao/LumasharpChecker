# LumasharpChecker v2（非 AI 版）

目前版本：2.0.0.1 / .NET Framework 4.7.2 / x64；以 LumasharpChecker 1.0.0.69 為基礎建立。

- 開啟「啟動v2.vbs」；固定執行 App/LumasharpChecker.exe。
- Source 保留目前原始碼；App 是獨立可攜執行目錄，包含既有 Recipe、Profile、設定與樣本快取的副本。
- 未帶入 .git、TestArtifacts、舊 bin/obj、CUDA、TensorRT provider、TensorFlow 原生 Runtime、Python 訓練環境和 AI 模型執行環境。GitHub 倉庫僅上傳原始碼與文件。
- 定位與 R3 工具共用 App/NativeR3 的非 AI InspTools.dll；不再強制載入 DeltaAIProcess/CUDA。保留部分原廠 DLL 的 .NET 相容參考，它們不代表已提供 AI 執行環境。
- 複製過來的 INI 已將 AI 定位、TensorRT、AI 增強、AI 預分類及 AIProphet 開關設為 False；原始專案與原廠 Recipe 未修改。之後匯入的 AI 定位 Recipe 會明確拒絕執行，須先停用 AI。
- v2 使用獨立的單實例識別，可以與原開發版區分。

## 建置

安裝 .NET SDK 後執行「建置v2.ps1」。定位元件依賴預設來自本機 App/NativeR3，發布不再複製 AI Runtime。編譯時請先關閉 v2。

## 本次確認

2026-10-30：Release x64 發布成功；主視窗為 1.0.0.69 且可回應。以 R002 一張樣本執行非 AI 原廠定位成功，框為 (9,129,1209,681)，約 74 ms。未完成八站、全部樣本、參數編輯／存檔與公司機台的完整驗收；這份是後續開發基底。

原資料夾約 19.2 GiB；v2 約 195 MiB（含原始碼與 App）。本次清理產生的重複編譯／預覽副本移到 Windows Temp，未刪除原專案。

## 第二輪精簡

已移除未使用的 AI 模型及主程式根目錄的原廠工具 DLL，改由定位與 R3 共用 NativeR3。修正建置內容傳遞，避免發布時再把定位元件 DLL 複製到主程式根目錄。原生定位試跑成功 (9,129,1209,681)，約 45 ms；主視窗啟動且可回應。共約 195.2 MiB / 218 檔；仍需完整功能驗收。精簡前 v2 副本保存在 Windows Temp，原始 LumasharpChecker 不變。


