# 設定儲存後自動複測 — 1.1.0.13

2026-11-03 使用者要求：定位或標準件設定完成後，自動測目前這張，讓主畫面工具介面立即有結果。

已完成：
- 原生定位／選向／選面工具成功儲存並同步後，自動呼叫既有單張複測流程。
- 主畫面右鍵設標準件成功後，自動複測。
- 光源設定頁追蹤成功儲存參數、設標準件或還原樣本；有儲存的視窗返回主畫面時，自動複測。沒有儲存則不觸發。
- 共用 RetestSavedImageAsync 核對配方、站別、主畫面圖片未切換後，將 busy 狀態交給既有 RunNativeRetestAsync(quiet:true)。
- 沿用工具勾選狀態、InspectFlowAsync 的單張預覽路徑與 ShowNativeResults；更新影像、工具狀態、耗時、亮度、結果明細，不累加批次統計。
- 儲存失敗不觸發；複測失敗以現有工具結果與流程訊息呈現，不撤銷已成功儲存的設定。定位未勾選或圖片不可用時沿用既有不可執行提示。

修改：MainWindow.xaml.cs、view/LightInspectionWindow.cs、LumasharpChecker.csproj、README.md。

驗證：WPF / .NET Framework 4.7.2 x64 Slim 發布成功，git diff --check 通過。檢視共用單張路徑確認 accumulate=false 僅保存 single-flow detail，不更動批次計數。未修改正式配方進行真實設定／複測端到端驗收。

部署：使用者確認關閉後，已替換 App 主程式與 PDB 並核對發布成品 SHA256；重新啟動顯示 LumasharpChecker 1.1.0.13，Responding=true。
發布驗證：C:/Users/juhnh/Documents/Codex/2026-11-03/new-chat/outputs/LumasharpChecker-1.1.0.13-發布驗證.json
