# LumasharpChecker Slim｜整體 UI／UX 設計交接

日期：2026-10-28　版本：1.0.0.43　平台：WPF / .NET Framework 4.7.2

## 參考設計與工具

- 以 CHAO2 MCP 直接檢視 `Chao8Side/src/Chao8Side.Main/SystemSource/UI/MainWindow.RecoveredBrightTheme.cs`，複用亮色工業 HUD 的視覺語彙；不複製相機／RTX／PLC 依賴。
- 共用 WPF `Source/LumasharpChecker/DesignSystem/HudTheme.xaml`，由 `App.xaml` 載入。所有非主畫面視窗使用相同的 Palette／Button／TextBox／ComboBox／GroupBox／DataGrid／TabControl／對話框標題／Focus／Disabled 狀態。
- 主畫面既有 `Window.Resources` 仍保留特定樣式，但已對齊全域配色。首頁保留 CCD01～08 卡片與深色影像區，不再放重複的設定按鈕。

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

- **v43 已完成** 單站右側「本站檢測摘要／進階判定設定」分層，避免將可編輯閾值預設顯示。尚未校準站明確標示；切換進入單站頁時重新收合，原有設定繫結與儲存命令沒有移除。
- **v43 GUI 驗證**：首頁→CCD1，進階預設 Collapsed；展開後「下限偏差 %」可見，收合後「儲存檢測設定」不可見；首頁→CCD2 時未校準明確顯示。實測視窗縮至 1130×740，單站摘要／進階區域仍可見並可操作，之後還原到 1440×900。
- **v43 隔離 UI 載入測試**：正式 App-net472-Slim 組件中「儲存檢測設定」（240px 高）、「統計分析結果」（1020×650）、「測試圖片結果」（1080px 寬；以空集合驗證）均可建立、顯示及關閉；不呼叫儲存／不改用戶 Recipe。統計視窗需使用以 App 檔案夾為基準的組件解析器載入 LiveCharts，避免 PowerShell 自身安裝目錄找不到 DLL 的測試環境假陰性。
- 仍需補 125%／150% DPI 多螢幕視覺檢查，以及正式統計圖與儲存設定的完整資料往返；此輪不宣稱已經完成。
- 統計視窗可加入匯出、樣本數與有效／失敗樣本清單；這是後續功能，不納入本輪。

**不變更**：原版 `LumasharpChecker`、Chao8Side、ChaoMcpHub、原廠母本及 production Recipe。
