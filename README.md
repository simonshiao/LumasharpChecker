# LumasharpChecker

亮度與銳利度檢查工具，包含 Windows WPF 桌面程式與一支 Cognex ROI 匯出 Python 工具。

## 專案內容

- `LumasharpChecker/`：WPF 應用程式原始碼、圖示及空白 LiteDB 初始資料庫。專案檔會將 `Resources/Mydata.db` 複製到建置輸出，因此此資源需保留在版本庫。
- `lumasharp_cognex_analyzer_ux_roi_export.py`：PySide6 / pandas 工具原始碼。
- `USER_GUIDE.md`：WPF 應用程式操作說明。
- `HANDOFF.md`：目前實作狀態、建置方式與待完成驗收清單。
- `config.json`、`app-settings.json`、`profile/` 與 `recipes/`：執行時的本機設定、使用者設定檔及多站 Recipe，不納入 Git。

## 開啟桌面程式（免系統安裝 Runtime）

雙擊專案根目錄的 **`啟動 LumasharpChecker.vbs`**。啟動器會使用共用的 `D:\0_軟體開發區\6.模板與共用資源區\dotnet-sdk-8\dotnet.exe` 直接執行 Release 的 `LumasharpChecker.dll`，並將工作目錄切到 Release 輸出目錄。

此電腦已具備 `Microsoft.WindowsDesktop.App 8.0.33`，不需重新下載或安裝 Desktop Runtime。直接雙擊建置出的 `LumasharpChecker.exe` 可能找不到這份共用 Runtime 而顯示安裝提示；請使用上述啟動器。

Agent 的完整啟動命令及驗收判準見 `HANDOFF.md` 的「正確啟動方式」。`dotnet.exe` 是程式主機，因此正常啟動後的程序名稱可能是 `dotnet`；應同時核對 DLL 命令列與主視窗版本。

## 多站 Recipe

- 在「設定」→「站別」選擇 8 站或 10 站；變更站數時不會刪除第 9、10 站資料。
- 上方工具列可選擇、載入、新增或關閉 Recipe；圖片下方的數字按鈕可切換站別。尚未載入 Recipe 時站別按鈕停用。
- 從「設定檔」選擇既有基準後，按「套用所選設定檔至本站」。程式會把設定與基準圖片複製到 Recipe，原設定檔不會被修改。
- 各站可獨立調整 ROI、亮度／銳利度門檻；按「儲存檢測設定」即可保存目前站別。
- Recipe 包含 `recipe.json` 及 `station01` 至 `station10` 目錄；已設定站別各自保存 `profile.json` 與 `pattern.jpg`。
- Recipe 和站數偏好保存在程式執行目錄下的 `recipes/` 與 `app-settings.json`。Recipe 的設定和基準圖可以整份複製封存。

## 建置 WPF 應用程式

需求：Windows、.NET 8 SDK。

```powershell
dotnet restore .\LumasharpChecker.sln
dotnet build .\LumasharpChecker.sln -c Release
```

建置輸出位於 `LumasharpChecker\bin\`，不納入版本控制。

## Python 工具

需要 Python、PySide6 與 pandas。從專案目錄執行：

```powershell
python .\lumasharp_cognex_analyzer_ux_roi_export.py
```

請依公司核准的套件來源安裝依賴；本專案目前未附 Python 鎖定檔。

## Git 注意事項

本機 `config.json`、`profile/`、`.vs/`、`bin/`、`obj/` 和 Python 快取都已排除。首次提交前，請確認 Git 使用者姓名與信箱已正確設定，並檢視 `git status` 中的檔案。
