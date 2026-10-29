# 本機交接

## 2026-10-30 光源全部樣本基準值／逐張亮度與 R3 選向選面開關

- Luma `LightInspectionWindow` 新增「計算全部樣本的基準值」按鈕：遍歷目前站別 `TestImageFiles`，每張執行 `LocateLightFrameAsync`，以定位框內同一套相對光源 ROI 取 HSV V 平均，計算成功張數的平均，顯示有效／失敗張數並帶入百分比模式 BaseValue 欄位。不將失敗圖片當作 0，全部失敗拒絕建立基準值。
- 上一張／下一張切換後，會試著以原生定位及光源 ROI 顯示「本張亮度」；未設定 ROI 或定位失敗顯示原因，不假裝有亮度。設定頁開啟不再預先強制執行 NativeRecipeBridge.RunPreLightAsync，讓使用者可以先校準。
- InspTools R3 `R3IsolatedEditor.EditNativeTool` 的直接選向／選面視窗新增「啟用選向／啟用選面」勾選，對應原廠 ParamFile 的 SelectWayON／SelectFaceON，並有「儲存 Recipe」呼叫已存在的 SaveNativeChanges (SaveAllRecipe、備份、重讀驗證)。目前仍是 Luma 啟用的 R3 工作副本，**不等於直接寫回來源原廠 Recipe**。使用者希望可修改原廠 Recipe，後續必須先明確設計可寫入目標與回讀驗證。
- Luma Release x64 0 errors／150 warnings，InspTools R3 Release x64 0 errors／0 warnings，關閉 Luma 舊 PID 46220、備份兩個 EXE 並更新測試目錄、新 Luma PID 44104 Responding=True。尚未完成 GUI 點擊驗收、全數 22 張定位成功率及原生開關存取驗收。

## 2026-10-30 光源設定解除 Profile Gate、切換圖片與純 ROI 單張試算

- `MainWindow.OpenAoiLight_Click` 已移除 `CurrentProfile == null` 的阻擋；前置原生流程本身仍執行，若失敗依既有機制中止，下一階段需將參數編輯和前置檢測分離。
- `LightInspectionWindow` 現在接 Luma `TestImageFiles` 的快照、目前圖片索引，新增上一張／下一張；光源 ROI 尚未設定時可開窗並顯示操作指引。自訂光源測試改用 `LocateLightFrameAsync` 和 `Measure` 的 HSV V ROI 量測，不再呼叫 `TestLightImageAsync / ImageProcessor.ProcessImage2` 的樣板比對結果。
- 未完成：原廠定位前置流程完全分離、舊 Profile 基準亮度 ±% 自動繼承、跨視窗即時同步、主畫面其餘檢測統一切換、真實定位/光源 OK NG 驗收。禁止誤報完整驗收通過。
- Release x64 0 errors／150 warnings；舊 PID 55588 已關閉、備份並更新測試 EXE、新 PID 46220 Responding=True。未覆蓋正式成品與原廠 Recipe。

## 2026-10-30 光源設定「百分比偏差／固定上下限」模式切換

- 研究結果：Luma 既有 `MainWindow.xaml` 亮度規格已使用 `ComboBox` 切換模式；`ProfilerManager.ApplyBrightnessThreshold` 的百分比公式為基準值 × (1 - 下限%/100)、基準值 × (1 + 上限%/100)。InspTools 的 `BinaryWindow` 也採 ComboBox 選單風格。未重造共用控件。
- `LightInspectionSettings.cs` 新增 `LimitMode`、`BaseValue`、`PercentLower`、`PercentUpper`、`EffectiveLimits()`，既有只有 Minimum/Maximum 的 JSON 預設以「固定上下限」相容讀取，避免無聲更改舊設定。
- `LightInspectionWindow.cs` 新增「百分比偏差／固定上下限」下拉切換；百分比模式提供基準值、下限偏差%、上限偏差%；固定模式沿用原本 Min/Max。自訂 ROI 的 `Measure()` 已依生效上下限做 OK／NG。百分比沒有有效基準亮度時會明確拒絕量測，避免假 OK。
- Release x64 0 errors／150 warnings。已備份／更新測試 EXE，啟動 PID 55588（Responding=True）。本次查無在相同測試路徑的執行中舊 Luma PID；正式成品與原廠 Recipe 未動。
- **未完成 Gate：** `MainWindow.OpenAoiLight_Click` 仍會在 CurrentProfile 為 null 時阻止光源設定開窗；`TestLightImageAsync` 仍走 ImageProcessor.ProcessImage2() 的樣板比對與舊判定。需下一階段將光源主流程完全切為定位 ROI + 光源 ROI + 亮度規格，且將原本的基準值／偏差設定轉入光源設定，不能宣稱已完成整套光源 OK/NG 整合。GUI 選單尚待實測。

## 2026-10-30 光源點檢參數流程研究（待改造）

- 使用者明確指定下一版以「原廠非 AI 定位 ROI + Luma 光源 ROI + 亮度門檻」作為單站光源檢測依據，不再使用 Luma 樣板比對；設定應集中於既有 `view/LightInspectionWindow.cs`，可直接選擇 Luma 已載入圖庫的圖片、切換上下張、畫 ROI、測試並儲存，不應因未建立 Profile／尚未校準就擋住設定視窗。
- 程式現況：`MainWindow.xaml.cs OpenAoiLight_Click()` 在 `CurrentProfile == null` 時直接阻止開窗；`LightInspectionWindow.RunLocalLightCheckAsync()` 呼叫 `mainViewModel.TestLightImageAsync()`，其要求 CurrentProfile 且進入 `ImageProcessor.ProcessImage2()`。舊 ProcessImage2 含樣板比對，現況並非純定位 ROI 光源測量。
- 既有可沿用：`LightInspectionSettings`（相對定位框 Left/Top/Right/Bottom、HasRoi、Minimum/Maximum）、`LoadLightSettings/SaveLightSettings`（每 Recipe/站別 light-inspection.json）、`LocateLightFrameAsync`、`LightInspectionWindow.Measure()` 的 HSV V 平均值。
- 下步先放行無 Profile 開窗，但正式 OK／NG 前應依 Recipe/站別取得定位框、轉換相對 ROI 並處理定位失敗；無光源 ROI 時允許開窗與選圖，但不得出具 OK 判定。需確認原生前置影像是否已傳入 Luma，避免假設原圖等於定位後影像。未修改程式／未部署，不能宣稱新流程已完成。

## 2026-10-30 選向／選面直開工具與 Luma 圖片清單

- Luma `MainWindow.xaml.cs` 的「選向」「選面」改傳 `TestImageFiles`、`CurrentTestImagePath` 至 `NativeRecipeBridge.OpenSandbox`，分別帶 `--open-select-way` 與 `--open-select-face`，不再只開原生 ToolsWindow。
- R3 `DecodedToolsWindowHost.cs` 沿用原版定位的隱藏宿主流程，`R3IsolatedEditor.cs` 新增 `OpenNativeTool`，分別建立原版 `SelectWayWindow`／`SelectFaceWindow`，並經原版 `SetInspection`、`SetBuffer` 顯示所選圖片。PageUp／PageDown 切換同一份 Luma 清單快照並更新索引；目前以原始圖及全圖矩形做預覽，**尚未把定位後 LocatedSrc／LocatedSrcTmp、LocateRect 接入**，不能宣稱整段原生定位到選面串接通過，也未驗證修改參數存回隔離 Recipe。
- InspTools R3 Release x64 0 errors／0 warnings；Luma Release x64 0 errors／150 warnings。已關閉舊測試 Luma PID 51496、備份 Luma 與 R3 EXE、更新測試資料夾並啟動新 Luma PID 3220（Responding=True）。未更動正式成品或原廠 Recipe。
- 使用隔離 Recipe `3923-R002` 與 3 張測試圖片分別做 R3 GUI smoke：`選向 - 1 / 3（PgUp／PgDn 切換）` PID 8896、`選面 - 2 / 3（PgUp／PgDn 切換）` PID 42404 皆成功顯示，沒有可見錯誤視窗。Luma 按鈕完整點擊、上下頁切換後影像內容、22 張全量驗證尚未完成。

## 2026-10-30 左側導覽名稱精簡

- 使用者要求將「八站總覽」改為「總覽」、「AOI工具」改為「工具」。已調整 `MainWindow.xaml` 的按鈕文字、ToolTip 與 AutomationProperties.Name，沒有變更命令或導航邏輯。
- Luma x64 Release 建置 0 errors／148 warnings。已關閉舊測試版 PID 39184、備份 EXE、部署並啟動 PID 51496（Responding=True）。正式成品和 Recipe 均未更動。

## 2026-10-30 左下統計列改固定高度、移除 OK／NG 百分比

- 依使用者畫面回饋，`MainWindow.xaml` 的左下 OK／NG／已判定／亮度統計卡改為 `Height=62`、`VerticalAlignment=Top`，不再跟右下角 155px 訊息區一起拉長。移除 OK/NG 兩個 `OverallOkRateText` / `OverallNgRateText` 顯示，保留數量及原有結果點擊。
- Release x64 0 errors / 148 warnings；關閉舊測試 PID 45940、備份 EXE、部署並重啟 PID 39184（Responding=True）。正式版／Recipe 未改。待 AOI 頁目視確認 62px 對齊效果。

## 2026-10-30 AOI 訊息文字框未填滿外層高度修正

- 回饋：右下訊息容器已增高但內部 TextBox 仍只顯示約兩行。保留外層 Grid Height=155，對 `AoiMessageLog` 明確設定 VerticalAlignment/HorizontalAlignment=Stretch、MinHeight=145，避免既有 TextBox 樣式垂直置中造成空白。
- Luma x64 Release：0 errors／148 warnings；關閉舊測試 PID 49976、備份 EXE、部署新版並啟動 PID 45940（Responding=True）。文字框實際高度尚待 GUI 目視確認。

## 2026-10-30 AOI 右下訊息欄精簡增高

- 依使用者要求，刪除訊息欄頂端「檢測訊息」標題與「複製」按鈕，只保留可選取／Ctrl+C 的唯讀多行 TextBox。訊息紀錄高度由 110 改為 155，沿用時間戳、捲動與訊息來源綁定。
- Luma x64 Release 建置 0 errors／148 warnings；關閉舊測試 PID 38012，備份並更新 TestArtifacts 測試 EXE，重啟 PID 49976（Responding=True）。正式成品與 Recipe 未修改，GUI 排版尚待目視驗收。

## 2026-10-30 AOI 訊息欄改為可選取／複製的歷史紀錄

- 依使用者參考之原廠 InspTools 訊息欄，改造 Luma `MainWindow.xaml` 右下角區域：原先的兩個不可複製 TextBlock 改為唯讀多行 `TextBox`，支援文字選取、捲動與「複製全部」按鈕。
- `MainWindow.xaml.cs` 新增 `AppendAoiMessage(source, message)`：加上時間戳與來源標籤、累積紀錄及限制字元數；原 `AoiWorkflow.PipelineStatusChanged` 和 VM `SaveSettingsMessage` 屬性更新均轉入此處。保留既有定位失敗報告路徑，未接入之原廠任意輸出不得宣稱已可收集；後續可由此入口接 R3 原生回傳之定位失敗與錯誤。
- x64 Release 建置 0 errors／148 warnings。關閉舊測試版 PID 55332、備份 EXE、部署新版並啟動 PID 38012（Responding=True）。不修改原廠 Recipe 或正式成品。右下角訊息欄實際 GUI 點擊／複製尚待驗收。

## 2026-10-30 AOI 流程說明移除，訊息改到右下角

- 移除 `AoiWorkflowPanel.cs` 工具卡下方的長文字流程說明；`SetPipelineStatus` 保留原呼叫點並改為 `PipelineStatusChanged` 事件，不再在按鈕區呈現長句。
- `MainWindow.xaml` 的左下統計卡只保留 OK／NG／已判定／亮度及分析進度；`SaveSettingsMessage` 搬到 AOI 工具右側下方新的淺藍灰「操作訊息」區，並由 `MainWindow.xaml.cs` 訂閱原生流程狀態更新顯示 `AoiPipelineMessage`。不修改檢測判定與 Recipe。
- Release x64：0 errors、148 warnings。已關閉舊測試程式 PID 46464、備份 EXE、更新測試目錄並啟動新 PID 55332（Responding=True）。尚待 GUI 視覺驗收右下角訊息卡的高度與換行。

## 2026-10-30 AOI 底部統計摘要改為左側淺藍灰卡片

- 依使用者回饋，將 `MainWindow.xaml` 的 OK、NG、已判定、亮度、進度與提示資訊從橫跨整個 InspectionPage（Grid.ColumnSpan=2）的深藍底部條，改為僅佔左側影像／控制區（Grid.Column=0），右側 AOI 工具欄不再被橫條覆蓋。
- 背景改為 `#EAF3F8`、邊框 `#C7DCE7`、文字深藍灰 `#24445A`；保留 OK/NG 的柔和綠／紅辨識，主要數值字級 24→18，資料綁定、結果點擊與進度功能未變。
- Release x64 建置：0 errors、148 warnings。舊測試 PID 24200 關閉、備份 EXE、更新 TestArtifacts 測試版，啟動 PID 46464（Responding=True）。未更新正式成品或修改 Recipe。仍需由使用者目視確認縮小後的摘要卡閱讀性。

## 2026-10-30 AOI 光源檢測移至二值化位置

- 依使用者要求，AOI 工具視覺流程應為「定位 → 選向 → 選面 → AI前處理 → 光源檢測（原廠燈泡節點位置）」；光源檢測取代原本二值化卡片，後方舊光源卡片移除避免重複。修改 `Source/LumasharpChecker/view/AoiWorkflowPanel.cs`，沿用 `LightRequested` 既有事件、`OpenAoiLight_Click` 與亮度檢測邏輯。
- 此次是工具卡排列／入口位置調整，不代表啟用 AI 前處理，也未更動原生定位、選向、選面及光源 OK/NG 計算順序。
- Luma x64 Release build：0 errors、148 warnings；先關閉測試版舊 PID 32324、備份 EXE、更新 `TestArtifacts/_luma_native_location/LumasharpChecker.exe`，重新啟動 PID 24200，Responding=True。正式成品、Recipe、InspTools R3 均未修改。
- 尚待 GUI 目視確認流程卡位置及操作點擊，不應宣稱完整影像／亮度 OK-NG Gate 已驗收。

## 2026-10-30 Luma 定位編輯預設忽略 AI 定位

- 使用者要求：即使原廠 Recipe 有勾 AI 定位，開啟 Luma 的定位編輯視窗也一律當未勾選處理。
- 在 `InspTools/src/InspTools.Standalone/R3IsolatedEditor.cs` 的隔離 R3 編輯入口，於建立 `LocateWindow` 前，將當前站別記憶體的 `ParamFile.AllowEnableAILocate=false` 和 `LocateParam.EnableAILocate=false`；取消舊版強制打開 AI 控件的寫法。不修改原廠 INI、vendor-source 或正式成品。
- Standalone x64 Release 建置 0 errors / 0 warnings；已備份並更新 `TestArtifacts/_luma_native_location/NativeR3/InspToolsStandalone.exe`，SHA256 `88C6426E23EC09686E2FC17A14DB7DF4CC220CFA38C4891049420A2D1F963F73`。Luma 主程式無須變更。
- 以 `--recipe-native-r3 3923-R002 --station 2 --open-locate` 開啟：PID 53108 正常存活，可見「定位」視窗，當次未列出 DeltaAIProcess 警告視窗。尚未驗證有 AI 定位勾選的其他 Recipe，亦不代表所有 AI 路徑永遠不會觸發。

## 2026-10-30 Luma AOI 圖片清單傳入定位編輯窗

- Luma AOI 點「定位」時，`MainWindow.xaml.cs` 現在傳入 `mainViewModel.TestImageFiles` 及 `CurrentTestImagePath`；`NativeRecipeBridge.OpenSandbox` 將目前可用圖片清單與索引寫在測試版 R3 的 `_luma_image_lists/<GUID>.txt`，以 `--luma-images`、`--luma-index` 啟動隔離 R3。現在定位使用的是啟動瞬間的 Luma 清單快照；在定位視窗開啟後，Luma 主畫面若換站或換圖不會即時雙向同步。
- R3 `DecodedToolsWindowHost` 讀入清單，`R3IsolatedEditor` 使用原版 `LocateWindow.SetImageSource(1, files, index,...)` 與 `SetBuffer(Mat)` 載入目前圖片。定位視窗可用原版內建的上一張／下一張機制；不另建第二份圖片 UI。
- InspTools.Standalone Release x64：0 errors/0 warnings；Luma Release x64：0 errors/148 warnings。已備份並更新測試版 Luma 和 R3，重啟 Luma PID 32324 且 Responding=True；正式版及 Recipe 未覆寫。
- 獨立 R3 入口以三張圖片清單做 GUI smoke：定位視窗成功開啟，沒有立即退出。**22 張實際 OK DB 全量操作、上下頁每張影像內容比對，以及從 Luma 圖示開啟後的畫面操作尚待實測；不能宣稱完整雙向同步。**

## 2026-10-30 Luma 定位直開（隱藏原生主介面）

- 使用者要求 AOI「定位」只顯示定位設定，不要先跳出 InspTools 主介面。
- 更新 `InspTools/src/InspTools.Standalone/DecodedToolsWindowHost.cs` 的 `--open-locate` 路徑：將原生 `ToolsWindow` 設為不出現在工作列、不取得焦點、透明；在 Loaded 啟動既有 R3 定位編輯時隱藏主視窗，定位編輯結束後關閉隱藏宿主。
- InspTools.Standalone Release x64 建置成功（0 warnings / 0 errors）。備份並只更新 Luma 隔離測試區的 `NativeR3/InspToolsStandalone.exe`，未覆蓋正式成品或 Recipe。
- 直接以 `--recipe-native-r3 3923-R002 --station 2 --open-locate` 做 GUI 測試，新 PID 34456 的可見視窗僅「定位」，不再顯示 INSPECTION TOOLS 主介面；Luma PID 54176 保持執行中。
- 尚待使用者由 Luma 點定位確認完整入口；選向／選面獨立參數窗、編輯持久化及完整光源 OK/NG 仍未驗收。

## 2026-10-30 定位圖示直開原版定位視窗（已部署、局部 GUI 驗證）

- 回應使用者回饋：AOI 點「定位」不能只開 InspTools 主介面。Luma `NativeRecipeBridge.OpenSandbox` 新增 `openLocate` 參數，定位按鈕帶 `--open-locate`；選向與選面仍為原 R3 工作區入口，**尚未直開對應設定窗**，不可誤報。
- 在 `InspTools/src/InspTools.Standalone/DecodedToolsWindowHost.cs` 新增獨立 R3 `--open-locate`：完成 `NativeR3RecipeSession.Open` 和 `R3IsolatedEditor.Attach` 後，視窗 Loaded/Dispatcher idle 自動呼叫既有 `R3IsolatedEditor.OpenStation(station)`，使用原廠 `LocateWindow`；主工作區仍在背後供 R3 隔離儲存。
- 補入 Standalone csproj 原本漏列的 `ChaoSelfAiBridge.cs` 與 `ChaoLocatePreviewUi.cs`；InspTools Standalone Release x64：0 warning/0 error；Luma Release x64：0 error、142 existing warnings。
- 舊檔先備份，再更新 `TestArtifacts/_luma_native_location/LumasharpChecker.exe` 和 `NativeR3/InspToolsStandalone.exe`，啟動新 Luma PID 54176 且 Responding=True。測試用直接執行 `InspToolsStandalone.exe --recipe-native-r3 3923-R002 --station 2 --open-locate`，GUI 視窗清單驗證同 PID 13404 同時出現 `INSPECTION TOOLS ... / S2 / 定位可編輯` 與獨立的 `定位` 視窗。
- **尚未驗收**：由 Luma 按鈕實際點擊的完整 GUI、自訂參數修改／保存、選向／選面直開，以及完整亮度 OK/NG。嚴禁覆寫正式成品與原廠 Recipe。

## 2026-10-30 AOI 三顆設定按鈕重新接回 R3（原始碼完成，部署待驗）

- 使用者指出 v1.0.0.69 的「定位／選向／選面」按鈕只執行 RunPreLightAsync，未開啟參數視窗；確認 MainWindow.xaml.cs 的事件綁定確實如此。
- 已修改 `Source/LumasharpChecker/MainWindow.xaml.cs`：三顆圖示改走 `OpenAoiNativeEditor`，以現成 `NativeRecipeBridge.Prepare + OpenSandbox` 開啟當前站別之隔離 R3 ToolsWindow。保留原生執行器及光源檢測入口不變。
- Release x64 net472 `dotnet build --no-restore` 已通過：0 errors、142 existing warnings。
- **驗收界線**：目前 R3 啟動器只支援 Recipe／station，尚無依按鈕自動選取「定位／選向／選面」子頁的已驗證參數；現版開啟的是同一個 R3 工具工作區，需使用者在視窗中選擇頁面。原始碼已修改但尚未將新 EXE 覆蓋正在執行的 TestArtifacts 版，也未完成三個 GUI 點擊驗證。不得宣稱 1.0.0.70 已部署。
- 下一步：檢查 R3 是否已有指定工具頁的介面能力；在隔離測試下完成入口頁定位、備份更新測試 EXE 並驗證 GUI。嚴禁覆寫 vendor-source、正式 App-net472-Slim、原廠 Recipe。

## 2026-10-30 連線恢復後續作：Luma 非 AI 原生 AOI → 光源點檢（v1.0.0.69）

- 使用者最終範圍：**定位 → 選向／翻轉 → 選面／認面 → Luma 光源檢測**。在原廠工作流燈泡節點的位置改用 Luma 亮度檢測；**不做 AI 定位、AI 前處理、AI 訓練或機台串接**。Luma 原有 UI、Recipe 匯入及光源 ROI 量測邏輯保留。
- 新增隔離的 `Source/LumaNativeFlowHost/` (.NET Framework 4.7.2 x64)：直接載入已逆向還原的 `InspTools.dll`，使用 `CVInspection.SetRecipe`、`CVInspection.SetBuffers`、原生 `LocateTool.DoLocate`、`SelectWayTool.Inspect`、`SelectFaceTool.Inspect`。原廠 Recipe 的 `EnableAILocate` **只在子程序記憶體**設為 false，不修改 INI；**不呼叫原生燈泡／LightMonitorTool／AI 推論**。支援子程序參數 `locate / way / face / light` 停在指定階段；以 JSON 回報各站實際狀態、AI 是否執行與錯誤，不覆寫原 Recipe。
- `NativeRecipeBridge.RunPreLightAsync` 先驗證 Luma `vendor-source` 與隔離的 `NativeR3/Recipe` 一致，再限時啟動非 AI 子程序及驗證回報（Recipe、站別、退出碼）；原生定位不通過時不允許進行光源 OK 判定。
- AOI 頁面「定位」、「選向」、「選面」三個入口已接到同一個原生子程序流程；原廠 Recipe 關閉的站別顯示 **Recipe 關閉／跳過**，不可誤報成功；「光源檢測」執行原生前置階段後才交回 Luma 的 `LightInspectionWindow`。避免多次連點造成並行子程序。保留其餘工具灰燈，不改 UI 版型。
- v1.0.0.69 和原生 Host **Release 建置成功（0 errors）**。部署並重新啟動 `TestArtifacts/_luma_native_location/LumasharpChecker.exe`，正式 `App-net472-Slim` 尚未更新。已檢查 `recipes` 下 36 個受保護檔案：**0 變更**。
- **離線影像實測**（原廠 Recipe `3923-R002`）：
  - S1 OK 圖：非 AI 定位 PASS，選向及選面均依 Recipe 關閉／跳過。
  - S2 OK 圖：非 AI 定位 PASS、選向 PASS、選面依 Recipe 關閉／跳過。
  - S3／S4：定位 FAIL；S6／S7：定位 PASS、選向 FAIL；S8：定位 PASS、選向 PASS、選面依 Recipe 關閉／跳過。上述失敗均未進入光源 OK 判定。
  - 完成 `LumaNativeFlowHost.exe` 的 S1 locate、S2 way／face／light 各子命令驗證；GUI Automation 點擊 v1.0.0.69 的定位、選向、選面及光源檢測按鈕，正確顯示上述狀態，主程式正常回應。確認 AI 執行狀態為 false。
- **重要未完成／嚴禁誤報**：目前 Luma `3923-R002` 八站的本地 `CurrentProfile`／光源 ROI 亮度上下限**尚未校準**，所以未驗收任何亮度 OK/NG；原生工具使用子程序自己的 `CVBuffer`，尚未把翻轉後的影像交給 Luma 亮度引擎，因此目前是**前置關卡與 Luma 檢測入口串接**，不是已完成「以原生翻轉後畫面直接量測亮度」的像素級閉環。後續應先處理影像座標／Buffer 契約，校準本站光源 ROI 與規格並用已知 OK／NG 圖驗收；不應把已有 AI 定位的原廠 Recipe 當成非 AI 同等效果。

## 2026-10-30 Luma 原廠 Recipe → InspTools R3 接線（v1.0.0.67）

- **範圍僅 Recipe**，不修改 Luma 既有 AOI 外觀、不重寫定位／翻轉／認面、不啟動 AI 推論或正式檢測；本次沒有修改獨立 `InspTools`／`Chao8Side`／Shunt 原始碼。
- Luma 原本已具 `RecipeManager.ImportVendorSnapshot/AttachVendorSnapshot`：將原廠資料複製到 `recipes/<LumaId>/vendor-source`，每檔 SHA-256 驗證；原廠路徑只留作來源記錄。
- 新增 `Source/LumasharpChecker/model/NativeRecipeBridge.cs`，從**目前 Recipe 的 vendor-source**讀取 `Setting.ini`、`station1～8.ini`，拒絕不完整、符號連結及不安全名稱；以保留原廠名稱的方式放到隔離 R3 `NativeR3/Recipe/<原廠 Recipe 名>`，檔案清單與全檔 SHA-256 比對，若已有同名不同內容則拒絕，**不覆寫**。
- 綁定既有 Recipe 管理視窗中的新增小按鈕 **「原生 Recipe」**，由 `mainViewModel.OpenNativeRecipeCommand` 呼叫橋接器 `Prepare`，再呼叫原有 `InspToolsRecipeCli.exe --recipe-check`，通過後以 `InspToolsStandalone.exe --recipe-native-r3 <Name> --station <N>` 開啟重建的原生 `ToolsWindow`。
- **R3 隔離環境**：從 `InspTools/output/InspToolsV4` 複製 28 個根目錄可攜檔案到 `TestArtifacts/_luma_native_location/NativeR3`；SHA-256 28/28 相同，`InspTools.dll` 的 SHA-256 為 `ACD6DBCF266705108021126B9E91C605D87500F21608DA70946F5CE25789C34B`，原始 `InspToolsV4` 的 Recipe、Model、TrainingImage、Runtime 不移動不覆寫。正式發布時需另將這個 R3 目錄加入發行包。
- 實測 Luma 既有 `3923-R002`（Luma ID `04fd36bc4dab450b94f5de90460a6279`），21 個快照檔案成功由原生 R3 Recipe CLI 驗證；第二次 Prepare 重複呼叫正常（不重複覆寫）；透過橋接器開啟 S1，原生 `ToolsWindow` 正常回應，標題 `R3 獨立版／真實八站／AI 未載入 ... S1`；關閉後直接以同一個原生啟動參數開啟 S2，正常回應。**R3 的 Portable Review 可能允許編輯隔離副本**，不是唯讀視窗；仍不會觸碰 Luma 的 vendor-source 或原廠來源。
- **GUI 驗收追加**：Luma 1.0.0.67 Release 建置 0 errors（142 個既有 warnings），測試程式已發布；由「選擇配方 → 原生 Recipe」實際點擊，成功開啟 `3923-R002 / S1` 的 R3 原生 ToolsWindow，程序正常回應。修正不同 GUI／Console 啟動情境下的 R3 CLI 輸出文字差異，改採 CLI ExitCode=0（原生 CheckStructure 成功）加逐檔 SHA-256 而非字串比對路徑。原廠來源、Luma vendor-source、隔離 NativeR3 三份檔案 **21/21 SHA-256 相同**。正式 `App-net472-Slim` 尚未含 NativeR3，需另安排正式發布。
- 本階段驗收的是 **Recipe 結構、完整檔案一致性、R3 原生八站物件初始化與站別切換入口**。後續定位 → 翻轉 → 認面 → Luma 光源檢測尚未整條驗收；`Inspect()`、模型載入、AI 推論、授權、Machine Host 都不在本階段，不得誤報通過。

## 2026-10-30 Luma 光源核心與 Shunt 接口復用（v1.0.0.66）

- 使用者要求**保持現有 Luma AOI 介面不動**，以光源點檢為首要功能；不把原版 InspTools 的 GUI 搬來取代 Luma，也不把 ShuntVC 及其 PLC/相機主程式搬進來。研究 Shunt 的 `ShuntInspectionToolsHost/CompiledReplicaHost.cs` 和 Adapter 工單驗收，採其「保留主 GUI、Recipe／站別準備、compiled ToolsWindow、回傳結果」的做法。
- 功能矩陣、已接線與尚未接線 Gate：詳 `AOI_SHUNT_REUSE_AND_FUNCTION_MATRIX_20261030.md`。切勿把停用的 AOI 節點美化成已完成。
- `1.0.0.66`：原 `LightInspectionWindow` 新增 **以本站參數檢測**，呼叫既有 `TestLightImageAsync`（本地樣板／亮度 ROI／HSV V／實際閾值），不依賴原廠 CheckAuth；保留「原廠定位（需授權）」、自訂 ROI 測試及儲存流程。未匹配時顯示定位失敗、不判 OK。
- AOI 流程中的「結果」接回已有結果視窗；「尺寸統計」根據真實檢測結果 ROI 像素值顯示最小／平均／最大，沒有測試結果時顯示提示，不產生虛假的毫米數值。
- 完整系統復線**尚未完成**：選向、選面、AI前處理、二值化、遮罩、尺寸量測、進階定位、AI訓練等仍缺 InspTools R3 站別／Recipe 接線和測試；原廠 CheckAuth 及 AI 模型推論仍未通過，不可擅自解除。

## 2026-10-30 Chao8Side 正確原廠對照／藍色 AI 定位頁（定位測試版）

- **唯一正確對照來源**：`E:\0_軟體開發區\1.成品區\Chao8Side`，不是先前誤用的另一份 DIA 原廠母本。參考此成品 `src/Chao8Side.Main/SystemSource/UI/MainWindow.RecoveredBrightTheme.cs` 使用的 `#18C7F4`、`#0078B8` 色系。
- Chao8Side 根目錄 `InspTools.dll` 與本測試 `NativeLocator/InspTools.dll` 的 SHA-256 相同：`85C4D87E59FB42520DE89B8C2AC04351E4701B809F21F8302F6266F4D96071D1`。差異在宿主開啟流程／UI 設定，不是版本差異。
- Chao8Side `src/Chao8Side.Host/OriginalAssemblyHost.LocateModelLifecycle.cs` 明確指出原廠 `LocateWindow` 內已有 AI 定位訓練 UI，但未開放 `AllowEnableAILocate` 時會被隱藏；原廠宿主可在執行期修改 UI 權限。
- Luma 不變更 `VendorAiLocateAccess.CheckAuthorization()` 的真實授權決策，也不強行啟用 `AllowEnableAILocate`；定位預覽內新增淺藍／藍色 **「AI 定位」唯讀頁籤**，顯示當前 Recipe 啟用狀態、模型名稱、真實原廠授權與模型載入狀態。未經授權時不啟用原廠 AI 推論、訓練或儲存。遠端訓練選項在此隔離定位預覽不顯示，與 Chao8Side 定位工作區的本地操作定位一致。
- 修改檔：`Source/InspToolsLocator/Program.cs`；x64 Release 建置 **0 errors / 0 warnings**。僅部署新的 `InspToolsLocator.exe` 至 `TestArtifacts/_luma_native_location/NativeLocator`，原版 EXE 已保留在 `_binary_backup/InspToolsLocator_before_chao8side_blue_ai_ui.exe`；`App-net472-Slim` 正式可攜版、原廠 Chao8Side、Recipe／Profile／Runtime 未改。
- **GUI 驗證**：由 Luma 1.0.0.65 → AOI 工具 → 開啟定位，原廠定位視窗持續運作；UI Automation 可見「簡易定位」「四邊定位」「電極定位」「AI 定位」頁籤。切換 AI 定位後顯示「原廠授權：未通過」「模型載入：尚未就緒」「CheckAuth 未通過」。未發現可見遠端操作控制；視窗正常回應。
- **驗收邊界**：這是保留授權關卡的 **AI 定位狀態預覽**，並非完整可編輯的原廠 AI 訓練頁；原廠 AI 訓練、模型載入與推論仍待合法授權／實機測試。

## 2026-10-30 AOI 導覽／OK DB 自動載入與原廠定位 GUI 修正（1.0.0.65）

- 依現場操作回饋，左側暫時隱藏舊「單站檢測」入口，保留內部檢測／ROI 邏輯。從「八站總覽」點選站別，或經既有舊單站入口，都進入 AOI 工具工作區；不再把操作者引到舊版單站頁。
- AOI 預設直接載入目前 Recipe、目前站別的 **OK DB**，未設定或無效時才提示設定圖庫；原本已在同站 OK DB 的圖片不重複清空。影像空白提示改為「載入 OK DB」，單張／資料夾檔案選擇按鈕暫時隱藏，但命令實作保留。
- 以實際 **3923-R002** 隔離資料驗收：從八站總覽選第 2 站，自動載入 **5 張 OK 圖**；回到第 1 站，自動載入 **22 張 OK 圖**。未彈出選檔視窗、未觸發正式檢測；AOI 入口與舊單站按鈕的可見性經 UI Automation 確認。
- 發現原廠定位 GUI 入口確實呼叫到 `InspToolsLocator` 並複製隔離 Recipe，但定位子程序約 2 秒後以 ExitCode 0 結束，無法留下定位視窗。根因是 `VendorAiLocateAccess.CheckAuthorization()` 建立並關閉臨時 `AuthWindow`，在 WPF 預設 `OnLastWindowClose` 下提前觸發應用程式 Shutdown。
- `Source/InspToolsLocator/Program.cs` 改為 `ShutdownMode.OnExplicitShutdown`，只在真正的 `LocateWindow.Closed` 時結束 GUI。重新部署後，從 **Luma AOI → 定位** 成功開啟持續存在的原廠定位視窗，實測視窗有 542 個 UI Automation 項目並能正常回應；標題及控件顯示 **「原廠授權檢查未通過（CheckAuth 未通過）」／「AI 定位：授權未通過」**，不是過去的 DLL 載入錯誤。
- **驗收界線**：DLL/Runtime 載入與原廠 GUI 生命週期已通過；授權檢查仍未通過，AI 模型載入與推論仍未驗收，不能將視窗可用等同 AI 已可檢測。不得繞過原廠授權。
- Luma .NET472 x64 Release **0 errors / 140 warnings**，InspToolsLocator x64 Release **0 warnings / 0 errors**。更新測試輸出 `TestArtifacts/_luma_native_location/LumasharpChecker.exe` 為 **1.0.0.65**，並單獨更新 `NativeLocator/InspToolsLocator.exe`；未重複複製 12 GB Runtime。舊二進位檔留在各自 `_binary_backup/`。測試版主程序保持正常運作，正式可攜 `App-net472-Slim/` 未更新。
- 本次未更改正式 Recipe、Profile、模型、資料庫與原廠母本；多次定位 GUI 試用會在 `NativeLocator/Recipe/luma_preview_*` 產生隔離副本，日後應加上安全的退出清理，避免測試資料無限制增長。

## 2026-10-30 Chao8Side AI Runtime 載入修正

- ProjectHub 共用狀態路徑 `D:\delta\專案進度` 在本機不存在；本次交接記錄於此專案檔案。
- 測試版目錄：`TestArtifacts/_luma_native_location/NativeLocator/`。其中 `Runtime/` 從 `E:\0_軟體開發區\1.成品區\Chao8Side\Runtime` 複製，43,539 個檔案、12,287,841,116 bytes；主要內容包括 `AIRuntime`、`TrainingRebuild`、`TrainingPython/Python38`、`CUDA11_118`、`DeltaAIProcess`、`ModelConverter`。
- Luma 發佈目標已新增 Runtime 複製步驟，部署腳本會在發佈時把來源 Runtime 複製到 `PublishDir/NativeLocator/Runtime`，並檢查 locator backend、Python、CUDA 與 DeltaAIProcess DLL。只驗證部署腳本與 MSBuild target 能對現有測試目錄完成同步；尚未做全新 publish。
- 原 GUI 截圖曾彈出「DeltaAIProcess載入錯誤，請檢查AI相關環境是否安裝正確。」根因是 `NativeDetection` 有設定 DLL 搜尋路徑，但一般 `locate` UI 路徑沒有；存在 Runtime 和 AppBase DLL 不代表 Win32 loader 能解析 CUDA/TensorRT 相依項。
- 新增 `VendorNativeRuntime.Initialize()`，在 GUI 建立 `CVInspection` 前，以及命令列定位前共用初始化：限制搜尋在 Windows 預設／應用程式目錄、加入 `Runtime\DeltaAIProcess` 與 `Runtime\CUDA11_118`，確認 AppBase 相容 DLL 與 Runtime canonical DLL SHA256 一致，再載入 AppBase 相容入口。載入失敗現在保留 Win32 error 和 DLL 路徑。
- Release locator build：0 warnings / 0 errors。已更新隔離測試版 `NativeLocator`；DLL SHA256 為 `3B056989D395E83B73AA2CD82EF3CB37DDA6DD8C50541DFD051636CA36DB5B38`。部署腳本也會檢查 AppBase 相容 DLL 與 Runtime 內 DLL 雜湊一致。
- 命令列定位在真實 Recipe `3923-R002` 和隔離影像上已越過 DeltaAIProcess 初始化，回報下一個原廠檢查結果 `CheckAuth 未通過`；因此 Runtime/DLL 載入 gate 通過，但目前授權仍阻止實際 AI 定位，模型推論尚未驗收。
- **尚未完成 GUI 複驗**：桌面控制助手讀得到 WPF 畫面，但點擊／鍵盤事件回報 `coordinate input geometry unavailable` / `foreground window did not report a process id`。重新啟動的 Luma 測試版目前在八站總覽；下一步在可操作的桌面環境按 AOI「定位 / AI定位」，確認原警告消失，然後另外驗原廠授權及模型載入。未改正式 Recipe、設定或資料庫。

## 2026-10-29 測試輸出集中整理

- 根目錄所有 `_` 開頭的暫存、驗證、預覽、備份與發布測試資料夾，以及 `outputs/`，集中移至 `TestArtifacts/`，保留原資料夾名稱與內容。
- 最新開發程式與原廠定位執行環境位於 `TestArtifacts/_luma_native_location/`；Recipe、設定、資料庫及定位引擎一併保留。
- 根目錄啟動器已改指向新位置；`Source/`、正式可攜版 `App-net472-Slim/`、相依檔 `External/` 保持原位。

## 2026-10-29 AOI 工具流程說明精簡（1.0.0.64）

- AOI 工具流程移除標題下方的操作說明和「第一排／第二排」方向提示，保留流程卡及連接線。
- Release 建置成功；根目錄「啟動減肥版」已透過啟動器驗證會開啟本機最新建置。正式可攜輸出仍為 1.0.0.62。

## 2026-10-29 AOI 工具流程精簡與啟動器修正（1.0.0.63）

- AOI 工具流程移除「前處理」「線段分析」「文字比對」，保留「AI前處理」及單一「二值化」。動態排版與蛇形流程線依剩餘 11 個節點重新排列。
- 根目錄 `啟動減肥版.vbs` 原先固定啟動舊的 `App-net472-Slim`，因此會看見舊畫面。現在優先啟動含原廠定位執行環境的 `TestArtifacts/_luma_native_location`，找不到時才退回正式可攜輸出。
- Release 建置 0 errors；更新開發輸出至 1.0.0.63，保留該目錄既有 Recipe、設定、資料庫及 `NativeLocator`。正式可攜輸出 `App-net472-Slim` 仍是 1.0.0.62，尚未更新為 1.0.0.63。
- 透過 `啟動減肥版.vbs` 實際啟動並確認主視窗 1.0.0.63；AOI 頁 accessibility 與畫面均確認三個移除項目不再顯示，AI前處理及二值化仍在。

## 2026-10-29 AOI工具右側流程試作（1.0.0.62）

- 側邊欄「AOI工具」改為在原有單站操作台右側顯示參考 InspTools 的流程示意；左側站別、圖片及正式檢測控制維持原功能。回到「單站檢測」會顯示原有判定範圍及參數設定。
- 只有定位測試開關與定位圖示可操作；圖示開啟獨立樣板定位測試視窗，其他節點為明確停用的佔位。定位未勾選時禁止啟動新的定位測試；執行中取消勾選不會中斷已開始的比對。可用目前單站測試圖作為初始目標圖，樣板／定位框、門檻與開關只在記憶體，不寫 Recipe／Profile、不控制正式檢測。不是 InspTools 原版 LocateWindow 或原版演算法，未接 AI。
- Release publish 成功，正式 `App-net472-Slim/LumasharpChecker.exe` 更新為 1.0.0.62，備份前版程式於 `_backup_before_1062/`；37 個設定、資料庫與 Recipe/Profile 檔案發布前後 SHA-256 相同。根目錄 VBS 已啟動，主視窗標題為 1.0.0.62 且程序回應中。
- 在 `_verify_1062/locator-smoke.ps1` 使用中文路徑合成圖呼叫實際定位程式：搜尋區內找到預期 `(50,35,24,20)`、分數 1，並略過一張過大的樣板。GUI 自動化 `sky.list_windows` 重試後仍回報 `codex app-server exited before returning response 1`；尚未目視驗收 AOI 流程、點擊定位圖示、畫 ROI 或用實際圖檔進行 GUI 比對。

## 2026-10-29 原版定位工具隔離試用驗證

- 原版定位視窗已在隔離環境開啟，載入 R002 第 1 站實際圖片；使用配方副本，關閉時不保存修改。程式回報視窗已開啟，但尚無桌面截圖確認完整顯示。
- 以第 1 站各一張 OK、NG 圖測試四邊定位，均取得定位座標；這僅證明找到定位區域，不代表良率判定或定位精度驗收。視窗載圖及「測試」呼叫成功；隔離配方中一個參數的修改、保存與重讀成功，原配方未變。
- AI 定位保持關閉，未測試。簡易、焊墊等其他模式、其他站別及全部設定仍待實圖驗證。
- 尚未整合進 Lumasharp AOI 工具頁。原版與新版的配方格式及影像處理元件不同，原版完整保存會寫入整站參數，直接使用正式配方有覆蓋設定風險。後續應以配方副本逐項確認參數及定位結果的交接方式，再評估整合；此隔離試用不代表已可接入正式檢測。

## 2026-10-29 AOI工具定位測試版（1.0.0.61）

- 側邊欄新增獨立「AOI工具」入口；工具可載入多張定位樣板、逐張啟用／停用與設定各自定位框，指定目標圖後執行 CPU OpenCV 樣板比對，回報最佳樣板、座標、相似度與門檻結果。目標圖可指定搜尋區，不指定則搜尋全圖。
- 此視窗是測試用途，樣板清單、ROI 及門檻只存在視窗記憶體；不接入正式檢測、不改寫 Recipe／Profile，也不使用 InspTools AI。採用現有 Emgu CV `MatchTemplate` 色彩正規化相關性作定位原型，尚未宣稱與 InspTools 演算法一致。
- Release 建置成功並發布；以 VBS 啟動時主程式為 1.0.0.61。當時桌面擷取連續逾時，未完成定位視窗的實際畫面與操作驗收。

## 2026-10-29 加入 InspTools GitHub 參考子模組

- `External/InspTools` 固定於 `simonshiao/InspTools` 的 `9b6f431d3ab7227ee92a97936debf85c29d75d63`，以子模組方式放入本開發樹；其獨立 UI 專案同樣以 .NET Framework 4.7.2／x64／WPF 為目標。
- 目前只作定位工具研究來源，未加入 LumasharpChecker 解決方案或正式建置。GitHub 主線沒有追蹤可直接參考的 `InspTools.dll`；上游定位視窗依賴 InspTools `CVInspection` 站別物件及 Recipe 參數格式，完整檢測 runtime 尚未驗收，所以不能直接當成 Lumasharp 的定位演算法套件。
- 不要把本機解譯 DLL 自動複製進來：現有 DLL SHA-256 `85C4D87E...` 與上游 R3 建置流程固定的 `ACD6DBCF...` 不同。後續需先確認目標是移植定位演算法，還是只參考其定位設定 UI，再接到多外觀參數流程。

## 2026-10-29 多外觀校準原型可試用版（1.0.0.60）

- 修正新增第一個外觀組後後續組名重複的問題，名稱會依組數接續編號。
- Release publish 成功；正式 `App-net472-Slim/` 更新為 1.0.0.60，更新前正常關閉本專案舊程序，46 個發布檔已更新，備份於 `_backup_before_1060/`。app-settings、資料庫、Nlog 與執行設定 4 個檔案 SHA-256 相同；Recipe/Profile 子資料夾未納入檔案複製範圍。
- 以 `--show-multi-appearance-preview` 開啟正式版試作視窗；桌面截圖確認雙欄版面、影像空狀態提示、組別與 ROI 操作區可見，主程式標題 `LumasharpChecker 1.0.0.60 [NET472 Slim]`，程序回應中。Release publish 無錯誤，未執行測試套件。
- 可建立外觀組、逐組選取／移除 OK 樣本、畫定位／亮度／銳利度 ROI、重設選取組 ROI、計算該組統計。試作狀態只在視窗記憶體，不寫入 Recipe、不參與正式檢測；正式多組參數保存與依命中外觀套用參數尚未完成。

## 2026-10-29 多外觀校準版面收尾（1.0.0.59）

- 修正外觀組標題列被星號列高拉伸、統計按鈕填滿工作列的問題；空白影像提示維持置中顯示。畫面現為左側外觀組與樣本、右側影像與統計的雙欄配置。
- Release publish 成功；正式 `App-net472-Slim/` 更新為 1.0.0.59，覆寫前正常關閉本專案舊程序，46 個發布檔已更新，備份於 `_backup_before_1059/`。`app-settings.json`、`MyData.db`、`Nlog.config`、執行設定檔雜湊相同；Recipe/Profile 子資料夾未納入檔案複製範圍。
- 以 `--show-multi-appearance-preview` 啟動正式版；桌面截圖確認試作視窗版面正常，主程式標題 `LumasharpChecker 1.0.0.59 [NET472 Slim]`，程序回應中。未執行測試套件。
- 本功能仍是記憶體中的互動原型，不寫入 Recipe、不參與正式檢測。正式多組參數保存、命中外觀後套用該組定位／ROI 與統計尚未完成。

## 2026-10-29 多外觀校準版面修正（1.0.0.58）

- 修正多外觀試作視窗右側工作區未配置到第二欄，以及外觀清單與標題列互相重疊的排版問題；無樣本時在影像區顯示加入圖片提示。
- Release publish 成功，正式 `App-net472-Slim/` 更新為 1.0.0.58；發布前正常關閉本專案舊程序，程式檔複製操作限定在輸出根目錄，設定檔與 JSON 雜湊有部分比對。備份於 `_backup_before_1058/`。
- 以 `--show-multi-appearance-preview` 啟動正式版；本機桌面截圖已確認多外觀試作視窗可見，主程式為 1.0.0.58。未執行測試套件。
- 多外觀試作仍只在記憶體操作，不寫入 Recipe、不參與正式檢測；正式多組資料保存、命中外觀後套用各組參數尚未完成。

## 2026-10-29 多外觀校準流程試作（1.0.0.57）

- 單站頁新增「多外觀試作」，可建立外觀組、選取／移除各組 OK 樣本、分別畫定位／亮度／銳利度 ROI，並以該組樣本計算亮度與銳利度統計。若目前站別有 OK 圖庫，會先顯示前 100 張供分組。
- 本版本為可操作流程原型：狀態只存在視窗記憶體，不寫入 Recipe、不影響現有新增／重設／檢測流程。正式多組資料保存、重設／編輯定向選組及依命中樣板套用對應參數尚未完成。
- 共用 .NET 8 SDK 的 Release publish 成功，正式 `App-net472-Slim/` 已更新 44 個程式檔；執行檔版本為 1.0.0.57。舊版正常關閉，覆寫前備份於 `_backup_before_1057/`。
- 發布前後比對 35 個 Recipe、Profile、設定與資料庫檔案的 SHA-256，全部一致。啟動正式 Slim EXE 的試作展示參數後，程序路徑正確、版本標題為 `LumasharpChecker 1.0.0.57 [NET472 Slim]` 且 `Responding=True`。
- 主視窗／試作視窗的畫面驗收未完成：Computer Use 擷取桌面逾時，故未確認實際可視畫面與樣本載入情形；未執行測試套件。一般啟動仍可從單站頁按「多外觀試作」開啟流程原型。

## 2026-10-29 OK／NG／ALL 主清單篩選（1.0.0.56）

- 底部 ALL／OK／NG 按鈕現在按檢測結果篩選單站主畫面的圖片清單。選 NG 後，清單、圖片序號／總數與前後張導覽都只包含 NG；ALL 顯示完整來源清單。右側 OK／NG 摘要卡保留開啟結果視窗的原功能。
- 篩選僅作用於瀏覽集合；連續測試、重新分析、完成狀態與匯出仍以完整來源圖片清單為準。重新分析完成後會依目前篩選重建清單。
- 共用 .NET 8 SDK 的 Release publish 成功，正式 `App-net472-Slim/` 已更新 44 個程式檔；執行檔版本為 1.0.0.56。舊版正常關閉，覆寫前備份於 `_backup_before_1056/`。
- 發布前後比對 33 個 Recipe、Profile、設定與資料庫檔案的 SHA-256，全部一致。根目錄 VBS 已啟動新版，觀察到正式 Slim 路徑的主視窗標題為 `LumasharpChecker 1.0.0.56 [NET472 Slim]`。
- 主視窗目視驗收未完成：Computer Use 的 `list_apps` 回報 `codex app-server exited before returning response 1`，因此沒有以畫面操作驗證篩選結果。未執行測試套件。

## 2026-10-29 單站連續測試提示修正（1.0.0.53）

- 實際檢查舊版畫面時，單站清單為 0 張；連續測試會在無圖片或無 Profile 時直接返回，原因只寫入未綁定畫面的 SaveSettingsMessage，因此按鈕看似無反應。無法據此排除使用者遇到其他情況。
- 無圖片或未校準時增加明確提示；狀態訊息顯示在單站結果區；開始時立即顯示準備進度，連續測試例外會顯示錯誤。保留本次作業前既有的未提交 ROI 顯示修改。
- 使用共用 .NET 8 SDK 在 _verify_1053 隔離發布成功。正常關閉依路徑辨識的舊 Slim 程序後，以隔離輸出的程式檔更新 App-net472-Slim，舊程式檔備份在 _backup_before_1053。更新前後 30 個 Recipe、profile、設定及資料庫檔案 SHA-256 一致。已透過根目錄 VBS 啟動，程序路徑指向正式 Slim EXE，版本檔資訊為 1.0.0.53。
- 未完成新版主畫面及按鈕的實際畫面驗收：Computer Use 在重新取得新版視窗後兩次擷取到其他程式，無法可靠操作。尚未在新版 GUI 載入真實圖片執行連續測試、暫停及續跑；不可將編譯或程序啟動視為這些流程通過。
2026-10-29：本機獨立建立 .NET Framework 4.7.2 Slim 版本，GitHub `net472-slim` 分支 commit `dc4ae0b8334da54dde72c020f1604d60cbfb01e8`，程式版本 1.0.0.52。原專案 .NET 8 工作目錄及未提交修改保留。

- 入口：根目錄 `啟動減肥版.vbs`；正式輸出：`App-net472-Slim`。本機已安裝 .NET Framework 4.8。
- 程式碼透過匿名大陸鏡像 `https://ghproxy.net/https://github.com/simonshiao/LumasharpChecker.git` 取得；NuGet 還原使用華為雲鏡像。勿將憑證提供給第三方鏡像。
- 原 .NET 8 Release 的 Recipe、profile、設定和資料庫已複製到新版資料夾，複製後啟動前比對 SHA-256 一致。新版資料是獨立副本，後續修改不會自動同步舊版。
- 已編譯並透過啟動器開啟，確認 1.0.0.52 八站總覽、T1 三站已設定及單站定位樣板視窗；第 1 站顯示 1/8 張主樣板，提供加入圖片功能。
- 尚未使用本機正、反面良品做實際定位與亮度判定驗證；多樣板沿用本站亮度判定範圍。實體相機連拍樣板尚未實作。





## 2026-10-29 側欄與圖片版面調整（開發輸出，未部署）
- MainWindow 導覽採獨立 NavigationRailButton 樣式，窄列 88px，圖示在上、名稱在下；以 Tag=Active 同步文字／圖示色彩及選中底色，加入 hover、pressed 與鍵盤焦點提示。
- 單站圖片區調整為中性深灰背景、12px 圓角與18px留白；未載圖顯示提示與開圖按鈕。空白時隱藏 Viewbox，避免遮住按鈕。
- Image 與 ROI Canvas 保留同一 Viewbox 等比例縮放，未修改演算法、Recipe 或光源邏輯。
- Release x64 建置通過，140 warnings / 0 errors；尚未完成實際視窗目視與 ROI 操作驗收，未覆寫 App-net472-Slim。
- 原始兩檔備份：`TestArtifacts/_backup_sidebar_20261029`。
