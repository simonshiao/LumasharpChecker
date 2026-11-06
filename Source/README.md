# LumasharpChecker 原始碼

目前正式版本為 **1.1.0.27**，包含 8 面操作介面與 AOI 系統。定位勾選時沿用既有定位流程；取消時將整張原圖作為參考座標，兩組 ROI／基準分開保存。無基準時仍可先保存 ROI 與允收範圍，光源檢測可於主畫面獨立啟用或略過。主畫面上方「說明」按鈕可開啟隨 App 部署的離線操作手冊。目標框架為 **.NET Framework 4.7.2 / Windows x64**。

版本依據以 `LumasharpChecker/LumasharpChecker.csproj` 的版本欄位及根目錄 README 為準。V2／V3 是舊專案稱呼，不是目前版本號。

- `LumasharpChecker`：WPF 主程式。
- `InspToolsLocator`：非 AI 原生定位宿主，元件取自本機 `App/NativeR3`。
- `LumaNativeFlowHost`：原生流程宿主來源碼。
- `LumasharpChecker/操作說明.html`：隨 App 部署的目前操作說明。
- `USER_GUIDE.md`：舊版操作說明備查。
