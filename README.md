# LumasharpChecker

亮度與銳利度檢查工具，包含 Windows WPF 桌面程式與一支 Cognex ROI 匯出 Python 工具。

## 專案內容

- `LumasharpChecker/`：WPF 應用程式原始碼、圖示及空白 LiteDB 初始資料庫。專案檔會將 `Resources/Mydata.db` 複製到建置輸出，因此此資源需保留在版本庫。
- `lumasharp_cognex_analyzer_ux_roi_export.py`：PySide6 / pandas 工具原始碼。
- `USER_GUIDE.md`：WPF 應用程式操作說明。
- `HANDOFF.md`：目前實作狀態、建置方式與待完成驗收清單。
- `config.json`、`app-settings.json`、`profile/` 與 `recipes/`：執行時的本機設定、使用者設定檔及多站 Recipe，不納入 Git。

## 多站 Recipe

- 在「設定」→「站別」選擇 8 站或 10 站；變更站數時不會刪除第 9、10 站資料。
- 左側「Recipe / 站別」可建立或載入整份 Recipe，並切換各站。
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
