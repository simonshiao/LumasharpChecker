# 全站共用最新圖片來源 — 1.1.0.17

2026-11-04 使用者要求：最新圖片設定不需逐站選 OK／NG／ALL，統一選一次，預設 OK 並讓 Recipe 記住。

已完成：
- Popup 移除每站分類 ComboBox，新增一個全站圖片來源，選 OK／NG／全部後立即保存。
- RecipeManifest 新增 RecentImageSource=OK。沒有新欄位的舊 Recipe 也預設 OK；舊逐站 RecentImageSource 留作歷史資料，最新圖片流程不再讀取逐站值。
- MultiStationInspection 使用 Recipe 共用來源；站別顯示狀態也由同一值建立。各站張數與批次勾選狀態保留。
- 切換 Recipe 更新共用 ComboBox，直接從 Recipe 狀態恢復，不在切換時覆寫其他配方。
- 正在執行檢測／原生參數操作時拒絕更改；寫入失敗回復來源與 UpdatedAt，介面回到原值。
- 保留所選分類無圖時既有的回退全部規則。

驗證：發布組件的隔離配方測試通過：預設 OK、NG／ALL 重載保存、不同 Recipe 隔離、各站張數保留、舊 Recipe 缺欄位預設 OK、寫入失敗回復與非法來源拒絕。WPF net472 x64 Slim 建置與 git diff --check 通過。未在正式配方內來回更改選擇做 UI 驗收。

部署：已正常關閉舊版，更新 App 主程式／PDB 並比對 SHA256，使用啟動.vbs 開啟 1.1.0.17。主視窗標題正確且 Responding=true，此為啟動驗證而非完整 UI 端到端驗收。
