# LumasharpChecker Slim｜整體 UI／UX 設計交接

日期：2026-10-28　版本：1.0.0.50　平台：WPF / .NET Framework 4.7.2

## 參考設計與工具

- 以 CHAO2 MCP 直接檢視 `Chao8Side/src/Chao8Side.Main/SystemSource/UI/MainWindow.RecoveredBrightTheme.cs`，複用亮色工業 HUD 的視覺語彙；不複製相機／RTX／PLC 依賴。
- 共用 WPF `Source/LumasharpChecker/DesignSystem/HudTheme.xaml`，由 `App.xaml` 載入。所有非主畫面視窗使用相同的 Palette／Button／TextBox／ComboBox／GroupBox／DataGrid／TabControl／對話框標題／Focus／Disabled 狀態。
- 主畫面既有 `Window.Resources` 仍保留特定樣式，但已對齊全域配色。首頁用 1～8 站卡片與深色影像區，不再放重複的設定按鈕。

## 設計原則

1. **檢測優先**：八站首頁一眼可見圖片、狀態、OK／NG 數與良率，只有開始／停止兩個主要動作；可選站測試。
2. **設定收納**：Recipe、圖庫路徑、ROI 與閾值回歸到各自的管理／單站視窗。
3. **保留真值語意**：首頁 OK／NG／良率是*實際判定*，不是圖庫分類數或產線良率。缺 Profile 則標示未校準。
4. **視覺一致**：淺冰藍背景、白卡、青色細框、深色影像窗；成功／失敗使用綠／紅，警告／未校準使用橙色；維持繁體中文。
5. **操作安全**：不改原廠資料、Recipe／Profile 儲存格式、既有 Command、計算引擎或 DLL。所有圖庫與參數重設繼續沿用原有邏輯。

## 共用主題 Tokens

| 角色 | 顏色 |
| --- | --- |
| Background | `#F0F8FC` |
| Surface / Subtle | `#FFFFFF` / `#F4FAFD` |
| Text / Secondary | `#0B2942` / `#527388` |
| Border / Focus | `#CBE4EF` / `#0078B8` |
| Cyan / Camera | `#18C7F4` / `#071828` |
| OK / NG / Warning | `#18B57B` / `#E95765` / `#F5A623` |

## 視窗覆蓋

| 視窗 | 本輪改動 |
| --- | --- |
| MainWindow | 單站頁跟首頁統一顏色、按鈕 Hover 狀態；首頁沿用 Chao8Side HUD |
| RecipeManagerWindow | 主標題、載入 Recipe 與完成動作樣式 |
| DatabaseSetupWindow | 主標題、匯入與完成動作樣式 |
| StationSettingsWindow | 標題與 ROI 相機視窗深色背景 |
| ProfileAddWindows | 重新安排操作與統計欄、深色影像工作區；保留原有畫 ROI／統計 Command |
| SavingProfileDialog | 由分散的英文 Save/Cancel 改為繁中、清楚的設定名稱欄與主次按鈕 |
| StatisticResultWindow | 統計視窗標題、底部關閉、亮度／銳利度分頁背景 |
| TestImagePickerWindow | 圖片選擇器配色、焦點與主操作按鈕 |
| TestImageResultsWindow | 檢測結果表格背景、標題、行／欄標頭視覺 |
| ResultImageDetailWindow | 放大看圖視窗背景與操作區配色 |
| ProfileDetailWindows | 由工程內部英文標題改成可辨識的繁中標題（此窗原本為空殼，尚無內容資料） |

## 驗收要求

- .NET472 x64 Release 建置、發布、載入全域 ResourceDictionary 不得例外。
- 逐一檢查：首頁、單站、Recipe、圖庫、ROI；結果列表與統計窗開啟時不得崩潰。
- 顏色優先於美感的**可讀性**：Primary 按鈕白字不能在滑鼠 Hover 時被換成淺色底；已修正按鈕的預設 Hover。
- 以 R002 真圖測試 CCD1：既有 11 OK／1 NG／91.7% 應與純 UI 版一致。
- 本輪不是原廠 AI 定位模型驗證，也不對 CCD1 漏判問題做品質宣稱。

## 後續 UX 工作

- **v50 圖庫回復路徑 UI**：撤銷 v49 圖片卡片與載入首圖的邏輯，恢復 1～8 站路徑清單，每站 OK／NG 路徑為唯讀可複製欄位，保留獨立資料夾選取與批次匯入。
- **v50 單張校準**：OK folder 有 1 張即直接開 ROI；新選資料夾至少 1 張；ROI 計算有效樣本下限 2→1，單張變異限制明示。Release 編譯 0 錯誤；使用一張真實 R002 圖片初始化 ROI ViewModel 成功；單點統計均值 251.5、sigma=0；圖庫 GUI 驗證 8 站、16 路徑欄、16 選取按鈕、0 圖片控制項。尚未進行實際單張 ROI→儲存→重開閉環（避免覆蓋現有 Recipe）。
- **v50 多站回歸**：目前 HOME-1 的 R002 為 5 站參與、3 站跳過，完成 20 OK／39 NG（33.9%），程式正常完成。此時 Recipe 校準狀態已與先前 1 站參與的測試不同，因此不可將兩次良率直接當作演算法回歸比較。
- **v49 圖像化圖庫**：取代純文字八站清單，以 4 欄 × 2 排視覺卡片容納 1～8 站。每卡片兩張 OK／NG 縮圖；直接點擊圖片開 Explorer 選取當前站別與分類。縮圖僅從資料夾載入第一張照片並限制 DecodePixelWidth=140，使用 OnLoad／Freeze 避免鎖住原始檔；來源不存在或沒有圖片時顯示資料夾圖示。保留先前對單站儲存、批次覆蓋確認、取消不變更的安全流程。
- **v49 GUI 驗收**：NET472 1.0.0.49 成功發布、啟動；UI Automation 確認 1～8 站、16 個可點擊 OK／NG 圖片控制項與 16 個圖片控制項。第 5 站 OK 點擊後原生 Explorer 顯示正確標題，取消前後 R002 recipe.json SHA-256 相同。尚未在正式 Recipe 實際換路徑並重開測試（避免覆寫使用者設定）。
- **v47～v48 圖庫路徑設定**：每站 OK／NG 可個別選取（Explorer IFileOpenDialog），只改動指定站別／類別，成功立即 SaveManifest；儲存失敗還原舊值。分類相反或 OK／NG 指向同一個資料夾會拒絕。批次覆蓋既有圖庫需確認，取消無資料寫入。原廠 TrainingImage 匯入入口保留並改為 Explorer。
- **v48 減法設計**：圖庫窗改成橫列清單，每站只顯示 1～8、OK／NG 路徑、可用狀態圓點、兩個選取鈕；主畫面與目前站別只顯示 1～8，不再加 CCD。移除影像視窗 IMAGE／NO IMAGE 等字樣，收短設定面板內容。
- **驗證界線**：NET472 x64 Release 建置 0 errors、1.0.0.48 主視窗啟動成功，UIAutomation 確認 1～8 數字及無 CCD 標題；圖庫可開啟。獨立路徑的「成功寫入→關閉→重開」尚待不影響現有 Recipe 的隔離驗收。
- **v46 導覽／功能分流**：使用者要求首頁與單站不出現在上方。正式移除頂部兩個分頁按鈕，左側只剩八站／單站導航；各站圖庫是工具對話窗，統一從頂部圖示開啟，不再在左側重複。頂部 5 圖示為 Recipe、圖庫、報表、結果、設定。齒輪面板錨定點改在圖示附近。正式執行驗證：頂部 5 圖示可見、頂部首頁／單站不存在、左側兩個導航可操作，設定可展開收合；Release build 0 errors。
- **v45 純圖示功能列**：回應 Chao8Side 現場截圖，移除頂部「LUMA」「光源點檢」文字及 Recipe 長文字按鈕，左側 7 個固定 48×45 圖示按鈕＋分隔線，右側為目前 Recipe、CCD 與執行狀態。功能依序為 Recipe 管理、八站總覽、單站檢測、各站圖庫、Excel 報表、單站結果、設定。所有圖示有原有 Command／Click 與 ToolTip／AutomationProperties.Name；報表與結果沒有假造多站匯出能力。設定面板靠近齒輪、當前頁面圖示顯示高亮。
- **v45 GUI 驗證**：正式 App 已啟動、7 個頂部圖示皆在正確序列且均可見、兩段品牌文字不再出現；1130×740 下各圖示與右側配方／CCD／檢測狀態仍可見。八站與單站切換及齒輪展開／收合已操作驗證。
- **v44 固定上方工具列**：原單站專屬 Header 改為橫跨整個視窗、Row0 固定 68px，全站首頁／單站都能使用。從左到右 LUMA、選擇 Recipe、實際已載入的 Recipe + 已設定站數、CCD 狀態、報表／檢測結果、檢測進度與狀態燈。原側欄 Recipe 重複入口移除，側欄自 Row1 開始；設定齒輪仍保留上方，展開的設定面板從 Row1 開始，不遮擋配方。
- **v44 GUI 驗證**：R002 顯示「已設定 1/8 站」、CCD1「已設定」、CCD2「待校準」；八站／單站切換上方控制項保持可見，Recipe 選擇按鈕開啟既有管理窗。縮至 1130×740，配方、站別與狀態元件仍可見；多站測試維持 CCD1 11 OK／1 NG／91.7% 且完成狀態同步於上方。
- **v44 全域狀態繫結**：上方的 `GlobalRunStatusText`／`GlobalStatusBrush` 依真正的 Recipe、多站測試、單站檢測與進度更新，不把「尚未測試」顯示為完成。對單站進度繫結已編譯，但尚未以實際長時間連續拍圖驗證所有過渡時機。
- **v43 已完成** 單站右側「本站檢測摘要／進階判定設定」分層，避免將可編輯閾值預設顯示。尚未校準站明確標示；切換進入單站頁時重新收合，原有設定繫結與儲存命令沒有移除。
- **v43 GUI 驗證**：首頁→CCD1，進階預設 Collapsed；展開後「下限偏差 %」可見，收合後「儲存檢測設定」不可見；首頁→CCD2 時未校準明確顯示。實測視窗縮至 1130×740，單站摘要／進階區域仍可見並可操作，之後還原到 1440×900。
- **v43 隔離 UI 載入測試**：正式 App-net472-Slim 組件中「儲存檢測設定」（240px 高）、「統計分析結果」（1020×650）、「測試圖片結果」（1080px 寬；以空集合驗證）均可建立、顯示及關閉；不呼叫儲存／不改用戶 Recipe。統計視窗需使用以 App 檔案夾為基準的組件解析器載入 LiveCharts，避免 PowerShell 自身安裝目錄找不到 DLL 的測試環境假陰性。
- 仍需補 125%／150% DPI 多螢幕視覺檢查，以及正式統計圖與儲存設定的完整資料往返；此輪不宣稱已經完成。
- 統計視窗可加入匯出、樣本數與有效／失敗樣本清單；這是後續功能，不納入本輪。

**不變更**：原版 `LumasharpChecker`、Chao8Side、ChaoMcpHub、原廠母本及 production Recipe。
