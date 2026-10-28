# LumasharpChecker｜.NET Framework 4.7.2 減肥版

這是從原專案 1.0.0.34 分出的**獨立移植版本**，目前是 **1.0.0.42 [NET472 Slim]**。目的是讓已具備 .NET Framework 4.7.2（或更新版本）的公司 Windows 電腦，不必額外安裝 .NET 8 Desktop Runtime，即可直接執行。

## 使用方式

1. 公司電腦需有 Windows x64 與 **.NET Framework 4.7.2 或 4.8**（已隨 Windows 安裝或原先存在）。本版本不會幫電腦安裝任何 Framework。
2. 將**整份** `App-net472-Slim/` 複製到公司電腦，不可以只複製 EXE。第三方原生 DLL 都必須跟著。
3. 在複製後的資料夾執行 `LumasharpChecker.exe`；在開發機，也可雙擊此目錄的 `啟動減肥版.vbs`。
4. Recipe、偏好和資料庫會寫入**該份程式所在目錄**，不與原版共用。

## 主畫面（1.0.0.35～1.0.0.42）

- 參考 `ChaoMcpHub` 的輕量收合式側欄，首頁預設為 **站別總覽**，側欄可切回 **單站檢測**。原先的單站 ROI、亮度／銳利度規格、OK DB、NG DB、本站圖片、連續測試及報表都保留在單站頁。
- 八站／十站與目前 Recipe 同步；總覽使用 4 欄卡片，逐站顯示**基準圖／圖庫樣本縮圖**及**本次檢測 OK／NG／良率**。來源只是 OK 樣本而非已校準 `pattern.jpg` 時會明確標註「未校準」，不會自動執行檢測。
- 點選總覽卡片會沿用原本的 `SelectStationCommand` 切換站別，並進入單站頁；避免另造一套 Recipe 或檢測器。
- 各站圖庫設定仍可由左側導覽開啟；單站頁保留 **OK DB／NG DB／本站圖片** 載入入口。這些設定操作不再佔用多站首頁。
- **1.0.0.36** 新增「TrainingImage 匯入」，支援 `R002-CCD1/Origional/1/OK|NG` 形式，從 CCD1 自動建立一份八站 Recipe；每站 OK 與 NG 各最多複製 6 張到 `TrainingImageCache/` 做可攜快取，記錄原廠 `Recipe` 以及 `Model/*.h5d/*.pbd` 路徑，**不視為已載入模型或檢測條件已校準**。
- **1.0.0.37** 單站檢測「重設參數」**左邊**新增「新增參數」；完成計算與統計結果確認後，**直接儲存到當前 Recipe 的 stationXX，無須先建立全域設定檔、再經 Recipe 視窗套用**。
- **1.0.0.39** 改為**無條件優先沿用目前站別設定的 OK 圖庫**：至少有 2 張圖片便直接使用其**全部圖片**進入 ROI 視窗，不再詢問、也不再限制前 30 張。重設參數沿用相同規則。
- **1.0.0.40** 首頁改為 Chao8Side 風格的極簡多站看板，側欄預設收合，移除大型標頭與重複的設定類按鈕。每站卡片顯示實際**判定 OK／NG 數量及良率**，右上角可勾選參與測試的站別。首頁只留 Recipe 名稱、測試彙總、進度與「測試勾選站別／停止」。
- **1.0.0.41** 直接參照 Chao8Side 實際主介面配色原始碼 `src/Chao8Side.Main/SystemSource/UI/MainWindow.RecoveredBrightTheme.cs`，以**亮冰藍／青色 HUD 主題**重新設計首頁：背景漸層 `#F7FCFF → #E6F6FF`、亮色標頭、細青框 `#79DDF7`、深色相機預覽視窗 `#071828 → #0A354A`、綠色 OK／紅色 NG。卡片使用 **CCD 01～08** 站名、即時測試狀態燈、三格 OK／NG／良率數值及細型良率條。僅取樣式，未導入 Chao8Side 的 RTX、PLC、相機 DLL，也沒有更動原廠母本。
- **1.0.0.42** 將 UI／UX 系統化：新增 `DesignSystem/HudTheme.xaml` 作 App 級共用主題，統一整支軟體的字體、表單、GroupBox、頁籤、DataGrid、色票、焦點與禁用狀態；Recipe、圖庫、ROI、儲存設定、統計與結果窗已逐頁套用，並修正新增參數視窗的浪費空間版型。細節與驗收清單參見 `UIUX_DESIGN_20261028.md`。
- 多站測試沿用現有 `ImageProcessor.ProcessImage2`，逐站使用獨立的 `profile.json`、`pattern.jpg` 及各站 OK／NG 圖庫影像，以 `IsOverallPass` 判定；圖庫原有的 OK／NG 分類不當成判定結果。沒有設定檔、沒有圖片的站會標為跳過，**不計入良率**。未測試時數量與良率顯示「—」，關閉應用程式後這輪記憶體結果不保留。
- 這份結果代表**離線測試影像的判定通過率**，不是經過外部真值比對的產線良率。
- **只有在本站未設定 OK 路徑、目錄失效或圖片少於 2 張時**，才跳出 Windows 原生 **檔案總管式資料夾選擇器**（IFileOpenDialog），支援網址列、貼上路徑、檔案總管瀏覽，取代傳統樹狀 FolderBrowserDialog；選到站號資料夾會自動尋找其 OK 子資料夾，明確禁止選取 NG 目錄。
- 使用者改選成功後會更新目前站別的 `OkDatabasePath`；若選的是程式包裡的 `TrainingImageCache` 則存**相對路徑**確保可攜。選擇器取消、或來源不足 2 張，皆不更新路徑／檢測設定。
- 按「新增參數」只開啟校準視窗，不會立即建立檢測 Profile；取消時維持未設定。已有本站 Profile／pattern 時會提示改用「重設參數」，避免覆寫。此功能未替使用者猜測原廠 AI 定位或光衰閾值。
- 尚未載入 Recipe 時，總覽保留站別卡片，但站別切換及圖庫設定不可操作；請先按「選擇配方」。
- `Chao8Side`／`ChaoMcpHub` 僅作為設計與操作參考，沒有修改或複製它們的 Runtime。

## 路徑

- **正式交付檔：** `App-net472-Slim/LumasharpChecker.exe`
- **正式啟動器：** `啟動減肥版.vbs`（自動切換到 `App-net472-Slim/` 工作目錄）
- **可維護原始碼：** `Source/`
- `App/` 是先前的 .NET 8 試驗包；`App-net472/` 是未清理的 4.7.2 比對包。**不要把這兩份當正式交付**。
- 原專案同層 `../LumasharpChecker/` 完全獨立，沒有變更其 Git、Release、Recipe 或執行設定。

## 實測容量

| 版本 | 檔案數 | 總容量 |
| --- | ---: | ---: |
| 原 .NET 8 Self-contained publish | 22 | 247.28 MiB |
| .NET Framework 4.7.2 初步輸出（含不必要平台檔） | 54 | 135.23 MiB |
| .NET Framework 4.7.2 精簡程式（不含資料快取） | 約 48 | 約 100.08 MiB |
| **目前 1.0.0.42 + R002 78 張離線樣本** | **程式 + 樣本** | **約 152.87 MiB** |

本版移除了 macOS 的 `*.dylib`、Windows ARM64 與 Windows x86 的原生套件。為避免破壞圖表及影像載入，保留 OpenCV / FFmpeg 與 Windows x64 SkiaSharp 相依檔案。

**R002 已完成的真實匯入結果與原廠 AI 路徑索引：** [TRAININGIMAGE_R002_SETUP_20261028.md](TRAININGIMAGE_R002_SETUP_20261028.md)。公司電腦僅需複製 `App-net472-Slim/`，不要只複製 EXE 或將來源 `D:` 路徑當成部署依賴；OK／NG 快取路徑已設成相對 EXE 的資料夾。

減肥版不需要 .NET 8，也不會在公司端下載 Runtime。若公司電腦的 Framework 版本低於 4.7.2，或禁止執行外部程式，仍需要請 IT 確認環境政策。

## 建置方式（在 Source 目錄）

```powershell
dotnet restore .\LumasharpChecker\LumasharpChecker.csproj --source https://api.nuget.org/v3/index.json -p:Platform=x64
dotnet publish .\LumasharpChecker\LumasharpChecker.csproj -c Release -p:Platform=x64 --no-restore -p:SlimPublish=true -p:DebugSymbols=false -p:DebugType=none -o ..\App-net472-Slim
```

目標 `net472`，所有相容性修改都只做在本專案的 `Source/` 中；`SlimPublish=true` 才會清理多餘平台的原生檔。

## 驗收紀錄（2026-10-28）

- **PASS**：.NET Framework 4.7.2 Release 編譯（0 個錯誤，136 個編譯警告；多為原有分析與 COM interop 警告）。
- **PASS**：正式 `App-net472-Slim/` 已發布，包含 R002 78 張離線樣本實測 152.87 MiB。
- **PASS**：主視窗標題 `LumasharpChecker 1.0.0.42 [NET472 Slim]`，ResourceDictionary 成功載入，8 張 CCD 卡片、勾選框、深色相機圖像區、OK／NG／良率與測試入口均在。
- **PASS**：新版 Recipe、圖庫及「新增參數」ROI 視窗實際開啟成功；正式配方／圖庫和校準結果未被改寫。統計與儲存視窗已編譯但尚未完成全操作路徑驗證。
- **PASS**：修復新良率進度條 `Value` 預設 TwoWay 嘗試回寫唯讀屬性造成的 WPF 啟動例外，改用 `Mode=OneWay`；重新發布後視窗成功啟動且持續有回應。
- **PASS**：新介面再跑 R002 測試：CCD1 仍為 11 OK／1 NG／91.7%，其他七站未設定而跳過；單擊第 1 站卡片可以進單站設定、按「八站總覽」返回；第 8 站勾選狀態可以切換且復原。
- **PASS**：R002 多站真圖測試：CCD1 使用已存在的 Profile 測試 12 張快取樣本（原始 6 OK + 6 NG），判定 **11 OK、1 NG、通過率 91.7%**；其餘 7 站尚未完成檢測 Profile，正確標為未設定且不計入。此結果暗示現有 CCD1 判定條件對來源 NG 樣本的攔截不足，尚待逐圖比對，不是正式產線良率。
- **PASS**：第 2 站有 5 張 OK 快取，點「新增本站參數」直接開啟 ROI「建立設定檔」，沒有前置圖庫選擇視窗；取消時未建立第 2 站 Profile。
- **PASS**：第 5 站沒有 OK 圖庫，點「新增參數」確實出現原生 Explorer-style `第 05 站 · 新增參數 · 選取 OK 圖片資料夾`，視窗是檔案總管型態（960×540），取消不保存。
- **PASS**：最後發布後重新開啟，按第 2 站「新增參數」直接出現 ROI 視窗，明確顯示「已載入 5 張基準圖片」；按第 1 站「重設參數」則直接顯示「已載入 6 張基準圖片」，兩次皆取消而未覆寫既有設定。
- **PASS**：R002 原廠 TrainingImage 匯入後有 7 站圖庫、78 張可攜快取；第 5 站沒有來源所以維持空白。第 2 站 OK 預覽成功顯示 1/5 張。
- **PASS**：不帶匯入參數重新啟動且從外部工作目錄啟動，仍恢復 R002 配方與相對路徑快取。
- **PASS**：八張總覽卡片可切換單站；雙頁沿用同一份 Recipe，設定類按鈕從總覽移至既有側欄／單站頁。
- **PASS**：減肥版使用獨立的單實例 Mutex，與原 .NET 8 正式版識別隔離。
- **PASS**：執行程序載入的是 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\clr.dll`，不是 .NET 8 的 `coreclr.dll`。
- **PASS**：原版 Git 工作樹無修改。
- **待測**：真正選取新 OK 來源後的路徑存檔及完整**統計值確認 → 寫入 stationXX → 重開讀回 → 影像檢測結果**閉環；公司實機驗證 .NET Framework 版本與 VC++ 原生相依；原廠模型檔格式解析、影像定位與亮度／銳利度檢測一致性、批次檢測和 Excel 匯出。此輪不宣稱已完成檢測演算法的跨版本回歸或原廠模型轉換。

**資料安全：** 減肥版未複製或修改原版私人 Recipe／資料庫，只另存 R002 測試 Recipe 和 78 張原廠來源樣本的副本。既有正式 Recipe 如需測試，請先複製到減肥版，勿改寫原版。
