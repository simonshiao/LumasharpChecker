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
- 上方「選擇配方」可建立、載入、關閉或刪除 Recipe，並在同一視窗選擇站別、從設定檔庫套用設定。建立 Recipe 後會立即載入，因此可以直接逐站設定。各站 OK／NG 圖庫路徑請使用主畫面的圖庫設定入口。
- 雙擊 Recipe 清單項目可直接載入；下次開啟程式會自動恢復上次載入的 Recipe。手動關閉目前 Recipe 後，下次啟動不會自動載入。
- 圖片下方的數字按鈕切換站別；主畫面提供「重設參數」按鈕，可選多張樣本、重新框選定位／亮度／銳利度 ROI 並重算基準。確認統計結果後更新目前設定檔或 Recipe 站別，保留原名稱。
- 設定檔庫位於「選擇配方」視窗，可建立、刪除或套用既有設定。套用時複製到 Recipe，修改本站不會覆寫設定檔庫原檔。
- 主畫面資料夾加齒輪圖示會開啟「各站圖庫設定」視窗，查看各站路徑並選日期資料夾、站號資料夾或站內 OK／NG 資料夾（例如 `welding\08\17\2`），一次為目前 Recipe 各站帶入 `<日期資料夾>\<站號>\OK` 與 `NG` 路徑。旁邊的資料夾圖示用於載入一般圖片。主畫面的「OK DB」及「NG DB」載入目前站別對應圖庫，切站時沿用所選圖庫類型。站號目錄內直接放置、沒有分類的圖片可用「本站圖片」載入。設定保存在 Recipe；缺少的目錄顯示不存在，不會自動建立或改動來源影像。
- 檢測結果列位於底部操作按鈕下方，只顯示 OK／NG 良率、已判定張數，以及目前預覽圖片的亮度值與相對變化；不顯示 ROI 或銳利度摘要。右側保留亮度／銳利度規格設定。
- 上方工具列提供選擇配方、匯出報表與檢視結果；齒輪保留全域設定。圖片下方控制列提供選擇資料夾／單張圖片、樹狀清單、前後張、連續測試及 ALL／OK／NG 結果篩選。資料夾或單張圖片選擇都只建立清單並預覽，不會自動開始檢測；良率卡片與篩選按鈕可開啟全部或指定判定的結果清單。
- 亮度與銳利度規格及儲存操作位於右側，和檢測結果共用可捲動區；移除獨立左側規格欄後，中央影像可用空間加寬。
- 連續測試仍逐張執行檢測；測試期間固定目前預覽，不逐張解碼或重繪，暫停或完成時才顯示最後檢測的圖片與結果。
- 檢測結果以並列的 OK 良率與 NG 不良率卡片呈現，各自顯示判定張數與百分比，並標示已判定總張數。
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
