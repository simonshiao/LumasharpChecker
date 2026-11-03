# LumasharpChecker Agent 接手規則

先讀 `README.md`、`HANDOFF.md`，再操作本專案。

## 開啟程式

- 正式程式為 `Source/LumasharpChecker` 的 .NET Framework 4.7.2 / Windows x64 專案；根目錄舊 `LumasharpChecker/` 與 solution 僅為歷史檔案，不作正式建置入口。
- 建置執行根目錄 `建置.ps1`；輸出在 `App/`，bin/obj 使用本次專用 TEMP 目錄並在結束時清除。
- 給使用者的入口是根目錄 `啟動.vbs`，開啟 `App/LumasharpChecker.exe` 並以 App 作工作目錄。
- 執行需要本機原廠 `App/NativeR3`、隔離標記與配方；公開原始碼不包含私有 runtime、樣本、原廠定位參數或使用者設定。
- 驗收要確認實際主視窗和功能區。只有 PID、`Responding=True` 或標題為 `LumasharpChecker.exe` 的視窗，不能證明主程式成功開啟。

## 版本及交接

- 使用者已授權每次更新完成後關閉本專案的舊版、更新正式 Release，再開啟新版供使用者查看。先以程式路徑／DLL 命令列辨識本專案程序，優先正常關閉並等候退出；不可批次終止其他 dotnet 程序。
- 將新版建置到啟動器指向的正式 Release 位置，保留原有 Recipe、profile 與使用者設定。用根目錄 VBS 啟動器開啟新版，確認版本及主畫面功能區後再交付；不要只建到獨立目錄便結束。
- 變更應更新版本與 `HANDOFF.md`，說明已完成、未完成和實際驗證結果。
- Git 網路操作使用使用者指定的大陸鏡像；可使用已連接的 GitHub 整合上傳，不把憑證交給第三方鏡像。
