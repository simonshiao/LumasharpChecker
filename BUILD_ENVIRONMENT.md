# LumasharpChecker 無 Visual Studio 建置環境

## 環境位置

共用資料夾：

`E:\0_軟體開發區\6.模板與共用資源區\LumasharpCheckerBuildEnvironment`

這裡放 Windows x64 可攜版 **.NET SDK 10.0.403**，包括 SDK 壓縮檔、解壓後的 `dotnet`、專案 NuGet 套件快取，以及建置啟動器。它只在執行建置的 PowerShell 程序設定 SDK 路徑，不安裝 Visual Studio、不修改系統 PATH，也不需要管理員權限。

本專案目標為 .NET Framework 4.7.2 / x64。主程式、定位宿主與流程宿主使用 `Microsoft.NETFramework.ReferenceAssemblies.net472` NuGet 套件提供編譯參考組件，不依賴 Visual Studio Developer Pack。這是建置用 SDK，不是 LumasharpChecker 的執行用 .NET Framework。

## 建置方式

預設專案路徑為：

`E:\0_軟體開發區\3.開發中專案區\1.進行中\LumasharpChecker-v3`

1. 確認專案有授權使用的 `App\NativeR3` 原廠檔案。這些檔案不包含在 SDK 套件中。
2. 正常關閉此專案正在執行的 LumasharpChecker。
3. 雙擊共用工具目錄內的 `Build-LumasharpChecker.cmd`。
4. 成功後，執行專案根目錄的 `啟動.vbs` 開啟新版。

其他專案路徑可在 PowerShell 執行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "E:\0_軟體開發區\6.模板與共用資源區\LumasharpCheckerBuildEnvironment\Build-LumasharpChecker.ps1" -ProjectRoot "D:\你的專案路徑\LumasharpChecker-v3"
```

輸出仍在專案的 `App\`；Recipe、profile 與使用者設定會保留。建置使用一次性的 TEMP bin/obj 目錄，結束後清理。

SDK 與目前專案需要的 NuGet 套件已放在共用工具資料夾。若專案增加或升級 NuGet 套件，還原新套件需要能連到 `nuget.org`。如果換電腦，請把整個 `LumasharpCheckerBuildEnvironment` 資料夾一起複製，以保留可攜 SDK 與套件快取。

## 驗證及官方來源

SDK 檔案 SHA-512 已依 Microsoft 官方 release metadata 驗證；完整值見套件中的 `SDK-SHA512.txt`。

- [.NET 10 SDK 官方下載頁](https://dotnet.microsoft.com/download/dotnet/10.0)
- [.NET Framework 開發套件與參考組件說明](https://learn.microsoft.com/dotnet/framework/install/guide-for-developers)
