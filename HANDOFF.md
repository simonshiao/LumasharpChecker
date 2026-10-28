# LumasharpChecker 開發交接／接手入口

## 2026-10-28 Recipe 工具列與圖片下方站別（1.0.0.11）

- 參考 DIAChip8Side 的上方 Recipe 入口與 InspTools 的圖片下方站別列，保留 LumasharpChecker 現有配色與檢測流程。
- 上方工具列提供「選擇 Recipe」、「新增 Recipe」、「關閉 Recipe」；選擇面板可載入或刪除所選配方。切換配方需按「載入 Recipe」。
- 圖片下方固定顯示 8 或 10 個站別按鈕，使用原有 `SelectStationCommand`、站別狀態與保存流程。未載入 Recipe 時按鈕停用。
- 程式版本更新至 `1.0.0.11`，調整 `MainWindow.xaml`、站別提示、README 與 USER_GUIDE；仍使用共用 SDK 啟動器。
- 使用共用 SDK 在獨立輸出目錄建置成功，0 個錯誤、128 個既有警告；已檢視實際主畫面，確認上方工具列及圖片下方 8 個站別按鈕顯示。
- Release 正式輸出建置成功，0 個錯誤、3 個既有套件相容性警告；建置前後確認正式 `MyData.db` 的 SHA-256 未變。透過根目錄 VBS 啟動後，已檢視 `LumasharpChecker 1.0.0.11` 主畫面與既有設定檔／基準圖片。
- 在獨立測試資料目錄建立測試 Recipe：確認關閉 Recipe 時站別停用、選擇 Recipe 後「載入 Recipe」啟用，載入後可切換至第 2、10 站。
- 實際操作設定的 10 → 8 → 10 站，核對 Recipe 的站數各次均寫入，並確認測試用第 10 站檔案保留、按鈕再次出現。以上使用獨立測試資料，非使用者正式 Recipe。
- 尚未以實際產品圖片完成全流程檢測與報表驗收；上一版啟動方式與其餘 Recipe 操作驗收清單見下方。

### GitHub 與本機歷史

- `1.0.0.11` 來源已由已連接的 GitHub 整合上傳；當時本機提交 `a32cc19` 與 GitHub 提交 `2dcfec27` 的完整檔案樹均為 `1f901372ba75fbf8f19199c946c6bbaa634a86e7`。先前未上傳的 `1.0.0.10` 啟動修正也包含在這份快照中。
- 首次上傳 GitHub 時建立了獨立提交歷史，因此本機與 GitHub 的提交 SHA／祖先不同；截至本次交接，本機尚未設定 Git remote。後續先核對來源、分支與未提交變更，再規劃歷史銜接，不要直接假設 `git pull --ff-only` 可用或強制覆蓋本機資料。
- Git 網路讀取依使用者要求使用大陸鏡像；上傳可使用已連接的 GitHub 整合。不要把憑證交給第三方鏡像。

## 2026-10-27 啟動修正（1.0.0.10）

- 共用 SDK 已包含 `Microsoft.NETCore.App`、`Microsoft.AspNetCore.App`、`Microsoft.WindowsDesktop.App`，版本均為 `8.0.33`。SDK 路徑為 `D:\0_軟體開發區\6.模板與共用資源區\dotnet-sdk-8`。
- 先前直接用 `Start-Process LumasharpChecker.exe`，沒有讓 apphost 找到共用 Runtime，開出的是 .NET 安裝提示。當時用 PID／Responding 判定「已開啟」不正確。
- 根目錄 `啟動 LumasharpChecker.vbs` 改為使用共用 `dotnet.exe` 直接執行 `LumasharpChecker.dll`，先檢查程式與 Desktop Runtime 是否存在，並固定 Release 工作目錄。
- `AGENTS.md` 與 `README.md` 已加入正確啟動入口。版本升為 `1.0.0.10`。

### 正確啟動方式

一般使用者：雙擊根目錄 **`啟動 LumasharpChecker.vbs`**。

Agent 可直接使用以下 PowerShell 命令；不需要管理員權限或系統安裝：

```powershell
$projectDir = 'D:\0_軟體開發區\3.開發中專案區\1.進行中\LumasharpChecker'
$sdkDir = 'D:\0_軟體開發區\6.模板與共用資源區\dotnet-sdk-8'
$outputDir = Join-Path $projectDir 'LumasharpChecker\bin\Release\net8.0-windows\win-x64'
$appDll = Join-Path $outputDir 'LumasharpChecker.dll'
$env:DOTNET_ROOT = $sdkDir
$env:DOTNET_ROOT_X64 = $sdkDir
Start-Process -FilePath (Join-Path $sdkDir 'dotnet.exe') `
  -ArgumentList ('"{0}"' -f $appDll) `
  -WorkingDirectory $outputDir -WindowStyle Hidden
```

`Hidden` 隱藏的是 dotnet 主機的主控台；WPF 程式自行顯示主視窗。成功判準是 **LumasharpChecker 目前版本**主視窗及 Recipe／設定檔／檢測區域出現。程序名稱可能是 `dotnet`；核對其命令列應指向上方 `$appDll`。

若失敗：先執行 `& (Join-Path $sdkDir 'dotnet.exe') --list-runtimes`，確認存在 `Microsoft.WindowsDesktop.App 8.0.x`，再核對 DLL、runtimeconfig.json 與工作目錄。直接開 EXE 出現的下載提示不代表共用 Runtime 缺失。

### 本次驗證

- 使用共用 SDK 執行 Release 建置成功：0 個錯誤、128 個既有警告。
- 從 `C:\Windows\System32` 啟動根目錄 VBS，確認啟動器能自行定位專案／SDK；實際程序為共用 SDK 的 `dotnet.exe`，命令列指向 Release 的 `LumasharpChecker.dll`。
- 已讀取程序載入模組：`coreclr.dll`、`PresentationFramework.dll`、`PresentationCore.dll` 均來自共用 SDK 的 `8.0.33` 目錄。
- 已擷取並檢視實際視窗：標題為 `LumasharpChecker 1.0.0.10`，可見 Recipe／站別、設定檔、基準圖片、亮度上下限與測試圖片功能區，並非 Runtime 下載提示。畫面證據保存在已排除 Git 的 Release 輸出內 `startup-1.0.0.10.png`。
- 多站 Recipe 的完整操作驗收仍見下方清單；啟動修正不代表已完成所有功能驗收。

## 2026-10-27 Recipe 與 8／10 站流程

- 新增 Recipe 管理與 8 站／10 站切換。Recipe 為一份完整產品設定；各站保存獨立的檢測設定與基準圖。
- 建立 Recipe 時建立 `station01` 至 `station10` 的資料夾骨架。切換 8 站時只隱藏第 9、10 站，不刪除其資料；之後切回 10 站可繼續使用。
- 從既有設定檔庫套用到本站時，會複製設定及基準圖到 Recipe；修改 Recipe 站別不會覆寫舊設定檔。
- Recipe 資料存於程式目前工作目錄的 `recipes/<Recipe ID>/`，站別資料為 `profile.json`、`pattern.jpg`；Recipe 索引與目前站數偏好存於 `app-settings.json`。上述本機資料已由 `.gitignore` 排除。
- 版本更新為 `1.0.0.9`。變更涵蓋 `RecipeManager.cs`、`StationSlotViewModel.cs`、`viewmodel/mainViewModel.cs`、`MainWindow.xaml`、`ImageProcessor.cs` 與 `ProfilerManager.cs`。
- `ProfilerManager` 支援相對基準圖路徑，讓複製整份 Recipe 時仍能解析站別內的 `pattern.jpg`；原有絕對路徑設定檔仍可讀取。
- 2026-10-27 使用 .NET 8 SDK 執行 Release 建置：**成功，0 錯誤，128 警告**。警告數與修改前的既有建置相同。
- **尚未完成實際 GUI 操作驗收**。建置成功只代表程式碼可編譯；Recipe 建立、切站、保存、切換 8／10 站及影像檢測仍須在桌面程式中逐項確認。

## 接手前先確認

1. 開啟正確專案目錄：`D:\0_軟體開發區\3.開發中專案區\1.進行中\LumasharpChecker`。
2. 讀取 `README.md`、`USER_GUIDE.md` 與本文件；確認目前 Git 分支與工作樹狀態。
3. Recipe 與偏好使用 `Environment.CurrentDirectory` 作為資料根目錄。既有啟動器會先切到 Release 輸出目錄；若更改啟動方式，需確認本機 `profile/`、`recipes/` 與 `app-settings.json` 實際寫入位置。
4. 執行建置：

   ```powershell
   & 'D:\0_軟體開發區\6.模板與共用資源區\dotnet-sdk-8\dotnet.exe' build .\LumasharpChecker.sln -c Release --no-restore
   ```

## 下一階段驗收清單

- [ ] 建立 Recipe、重開程式後載入 Recipe。
- [ ] 將舊設定檔套用至第 1 站，確認設定與基準圖複製到 Recipe，原設定檔不變。
- [ ] 修改站別 ROI／門檻並儲存；切換到其他站再切回，確認各站設定互相獨立。
- [ ] 切換 8 站與 10 站，確認第 9、10 站資料保留，10 站模式仍能載入。
- [ ] 分別執行資料夾批次檢測及單張圖片檢測，確認使用目前 Recipe／站別設定並產生預期報告。
- [ ] 將 Recipe 資料夾複製到另一個工作目錄，確認相對基準圖可載入。
- [ ] 確認 Recipe 刪除只移除所選 Recipe，舊設定檔庫不受影響。

## 資料與版本控制界線

- `.gitignore` 排除 `config.json`、`app-settings.json`、`profile/`、`recipes/`、建置輸出、IDE 狀態及 Python 快取。
- `LumasharpChecker/Resources/Mydata.db` 是目前追蹤中的空白 LiteDB 初始檔；執行資料庫位於程式輸出目錄。
- 不要把使用者的 Recipe、基準影像、私人設定或執行期資料加入 Git。若要交接 Recipe，請由使用者另行匯出／複製並確認可分享範圍。
- 本次已驗證建置；未宣稱完成 GUI 或跨機驗收。每次後續交接應更新本文件中的日期、變更、建置結果與尚未通過的驗收項目。

## 交接結論

8／10 站的 Recipe 資料模型、站別設定隔離、舊設定檔複製套用、相對基準圖解析、介面與文件已完成，Release 建置成功。下一位接手者應優先完成上方 GUI 閉環驗收，再依實測結果修正任何站別保存、影像路徑或報告差異。
