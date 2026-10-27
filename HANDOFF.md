# LumasharpChecker 開發交接／接手入口

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
