# 光源 ROI 初始化修正 — 2026-11-03

問題：既有光源設定採相對於定位範圍的固定像素邊距。配方 2222 第一站的 Left=1683、Right=1642、Top=216、Bottom=216，當目前定位寬／高不足以扣除邊距時，GetRoi 直接拋錯。載入頁面中止，HitRoi 又回傳 None，因此既沒有可見框，也無法拖曳重畫，側面窄尺寸尤其容易遇到。

修正：
- LightInspectionSettings 增加編輯專用 EnsureEditableRoi／ResetRoi：有效既有框保持原值；未設定、邊距過大或不足 2×2 的框，依目前定位尺寸建立中央一半範圍，最小 2×2。無效原生定位範圍明確報錯。
- 光源頁載入影像時先確保有可編輯框，重建後保持未儲存狀態並提示重新設標準件，不用舊基準自動判定。
- 增加「重設 ROI」按鈕；無效框也能在定位範圍內開始拖曳重畫，不再因 HitTest 失敗卡死。
- 新配方各站光源 ROI 改為等待實際定位尺寸初始化，不再複製 t1 的固定像素邊距。
- GetRoi 正式檢測驗證仍嚴格，不自動裁切或改動已儲存檢測範圍；未批次修改真實配方。
- 還原樣本時若定位尺寸不適用，保留重建框的未儲存提示。

驗證：
- 獨立測試通過：以真實過大邊距模擬窄定位區；有效框不變；2×2、奇數尺寸、橫／直窄側邊；int 最大邊距不溢位；無效原生尺寸拒絕。既有樣本保存、還原與配方交易測試也通過。
- .NET Framework 4.7.2 x64 Slim 發布通過，git diff --check 通過。
- 使用者儲存關閉後，已更新 App 主程式與 PDB，SHA256 與發布成品一致。
- 新版已重新啟動，視窗 LumasharpChecker 1.1.0.9 且 Responding=true。
- 未在真實配方按儲存／設標準件做 UI 驗收；使用者可直接開光源頁檢查新框並調整。

修改檔案：LightInspectionSettings.cs、LightInspectionWindow.cs、NativeRecipeBridge.cs、diagnostics/LightSampleStorageChecks/Program.cs、README.md。
測試紀錄：C:/Users/juhnh/Documents/Codex/2026-11-03/new-chat/outputs/LumasharpChecker-ROI初始化驗證-20261103.log
更新紀錄：C:/Users/juhnh/Documents/Codex/2026-11-03/new-chat/outputs/LumasharpChecker-ROI修正App更新驗證-20261103.csv
