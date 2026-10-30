# LumasharpChecker v2（非 AI 版）

目前版本：2.0.0.1 / .NET Framework 4.7.2 / x64；以 LumasharpChecker 1.0.0.69 為基礎建立。

- 開啟「啟動v2.vbs」；固定執行 App/LumasharpChecker.exe。
- Source 保留目前原始碼；App 是獨立可攜執行目錄，包含既有 Recipe、Profile、設定與樣本快取的副本。
- 未帶入 .git、TestArtifacts、舊 bin/obj、CUDA、TensorRT provider、TensorFlow 原生 Runtime、Python 訓練環境和 AI 模型執行環境。GitHub 倉庫僅上傳原始碼與文件。
- 定位與 R3 工具共用 App/NativeR3 的非 AI InspTools.dll；不再強制載入 DeltaAIProcess/CUDA。保留部分原廠 DLL 的 .NET 相容參考，它們不代表已提供 AI 執行環境。
- 複製過來的 INI 已將 AI 定位、TensorRT、AI 增強、AI 預分類及 AIProphet 開關設為 False；原始專案與原廠 Recipe 未修改。之後匯入的 AI 定位 Recipe 會明確拒絕執行，須先停用 AI。
- v2 使用獨立的單實例識別，可以與原開發版區分。

## 建置

新增 Recipe 使用 `App/NativeR3/RecipeDefaults` 的原廠參數樣本補足相容欄位；此資料夾隨本機 R3 部署，不納入原始碼倉庫。缺少模板時會明確報錯，不會建立不完整的配方。樣本的機台參數僅供線外相容，各站仍需設定與校準。

安裝 .NET SDK 後執行「建置v2.ps1」。定位元件依賴預設來自本機 App/NativeR3，發布不再複製 AI Runtime。編譯時請先關閉 v2。

## 本次確認

2026-10-30：Release x64 發布成功；主視窗為 1.0.0.69 且可回應。以 R002 一張樣本執行非 AI 原廠定位成功，框為 (9,129,1209,681)，約 74 ms。未完成八站、全部樣本、參數編輯／存檔與公司機台的完整驗收；這份是後續開發基底。

原資料夾約 19.2 GiB；v2 約 195 MiB（含原始碼與 App）。本次清理產生的重複編譯／預覽副本移到 Windows Temp，未刪除原專案。

## 第二輪精簡

已移除未使用的 AI 模型及主程式根目錄的原廠工具 DLL，改由定位與 R3 共用 NativeR3。修正建置內容傳遞，避免發布時再把定位元件 DLL 複製到主程式根目錄。原生定位試跑成功 (9,129,1209,681)，約 45 ms；主視窗啟動且可回應。共約 195.2 MiB / 218 檔；仍需完整功能驗收。精簡前 v2 副本保存在 Windows Temp，原始 LumasharpChecker 不變。



## 原廠配方自動匯入

在配方管理按「自動匯入」從 `C:\DIAChip8Side\Recipe` 匯入；首次匯入後，啟動時檢查該來源是否新增或變更，並詢問是否更新本機副本。站別設定及更新前的副本會保留；同名但來源不同的配方不會自動覆蓋。其他路徑可在「配方管理」選「其他匯入」，先比對再匯入。匯入不修改原廠資料，亦不等同完成亮度／銳利度參數設定。

## 2026-10-31 定位流程效能修正

定位 → 選向 → 選面現在共用常駐的隔離 `LumaNativeFlowHost --worker`，不再逐張重開程序與載入 Recipe。首張仍有初始化成本；切站、切配方或參數內容改變時重載。每張仍比對來源與原生副本的完整 SHA，只有內容與 Runtime 未變時重用 CLI 驗證結果；不接受副本不一致。

每張開始前還原原廠工具會修改的座標與旗標。預覽影像以無損 BMP 回傳，結果節點顯示整張流程耗時。主程式結束／開啟原生編輯器時關閉 worker；中斷、逾時或協定異常後，下次檢測重新建立。診斷與完整流程一致性測試見 `diagnostics/WorkerRegression`，先前原因分析見 `diagnostics/LocatePerformance/REPORT_20261031.md`。

後續顯示修正：結果節點改為四項工具的「檢測合計」，整體耗時另列，游標停在時間上可看配方、原生往返、清單、預覽與訊息的分項。連續檢測過程保留目前影像，只更新進度，結果暫存於記憶體；完成或停止後一次發布清單與統計、載入最後一張已完成圖片。批次最後畫面更新另記耗時，不混入該張工具計時。
