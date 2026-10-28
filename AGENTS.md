# LumasharpChecker Agent 接手規則

先讀 `README.md`、`HANDOFF.md`，再操作本專案。

## 開啟程式

- 此電腦共用 .NET 8 位於 `D:\0_軟體開發區\6.模板與共用資源區\dotnet-sdk-8`，已包含 Desktop Runtime；優先使用這份環境。
- 給使用者的入口是根目錄 `啟動 LumasharpChecker.vbs`。它使用共用 `dotnet.exe` 執行 Release 的 `LumasharpChecker.dll`。
- Agent 直接啟動時，使用 `HANDOFF.md` 的完整命令，指定 Release 輸出為工作目錄，讓設定與 Recipe 寫到正確位置。
- 單獨雙擊 `LumasharpChecker.exe` 可能只開出 .NET 安裝提示。若出現提示，先確認共用 SDK 的 `--list-runtimes` 及啟動命令；不要直接重複下載 Runtime。
- 驗收要確認實際主視窗和功能區。只有 PID、`Responding=True` 或標題為 `LumasharpChecker.exe` 的視窗，不能證明主程式成功開啟。

## 版本及交接

- 變更應更新版本與 `HANDOFF.md`，說明已完成、未完成和實際驗證結果。
- Git 網路操作使用使用者指定的大陸鏡像；可使用已連接的 GitHub 整合上傳，不把憑證交給第三方鏡像。
