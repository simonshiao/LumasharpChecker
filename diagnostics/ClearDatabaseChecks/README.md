# 清除圖庫交易驗證

獨立 net472 x64 主控台，以反射呼叫實際發布成品的 RecipeManager，使用 artifacts 目錄內新建的配方，不動正式配方與圖片。

參數：受測 LumasharpChecker.exe、依賴 DLL 所在 App 目錄。使用 `dotnet run --project diagnostics/ClearDatabaseChecks/ClearDatabaseChecks.csproj -c Release --artifacts-path <本次暫存目錄> -- <受測EXE> <App目錄>`。

涵蓋單一分類清除及 ALL 覆寫移除、整站清除、不影響其他站／標準件／原圖、重載持久化、空站 no-op、鎖定 manifest 寫入失敗回復、範圍外站號拒絕。
