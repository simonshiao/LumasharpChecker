# LumasharpChecker Slim｜R002 原廠 TrainingImage 匯入交接（2026-10-28）

## 成品及資料隔離

- 成品：`App-net472-Slim/LumasharpChecker.exe`，版本 **1.0.0.36 [NET472 Slim]**。
- 此輪新增的是一個**可攜離線圖片測試 Recipe**，不是已通過原廠 AI 定位校準的完整檢測 Recipe。
- Recipe 顯示名稱：`R002（TrainingImage）`；存檔在 `App-net472-Slim/recipes/0ec2cc498bac4505aac52fe39dfa4682/recipe.json`。
- 原程式 `../LumasharpChecker/`、原廠 `D:/0_軟體開發區/6.模板與共用資源區/原廠母本/DIAChip8Side_Welding/` 皆未寫入。
- 來源掃描入口：`TrainingImage/R002-CCD1/Origional/1`，也可以選 `R002-CCD1`。程式往上找到 `TrainingImage` 再依 `R002-CCD1～8` 辨識所有站別。

## 匯入規則

```
TrainingImage/R002-CCD1/Origional/1/OK (22)
TrainingImage/R002-CCD1/Origional/1/NG (8)
TrainingImage/R002-CCD2/Origional/2/OK (5)
TrainingImage/R002-CCD2/Origional/2/NG (165)
... CCD3、4、6、7、8 同型 ...
```

以上資料夾名的 `Origional` 是原廠實際拼字；程式也接受正常的 `Original`。只辨識該產品的確切 `R002-CCDn` 目錄，不自動混入附帶 `-0624` 或 `-DW` 的其它訓練版。

| 站別 | 原廠 OK | 原廠 NG | 離線快取 OK | 離線快取 NG |
|---|---:|---:|---:|---:|
| 1 | 22 | 8 | 6 | 6 |
| 2 | 5 | 165 | 5 | 6 |
| 3 | 15 | 48 | 6 | 6 |
| 4 | 10 | 81 | 6 | 6 |
| 5 | 0 | 0 | 0 | 0 |
| 6 | 1 | 102 | 1 | 6 |
| 7 | 41 | 132 | 6 | 6 |
| 8 | 6 | 134 | 6 | 6 |
| **合計** | **100** | **670** | **36** | **42** |

- 快取：`App-net472-Slim/TrainingImageCache/R002/{1..8}/{OK,NG}`，共 **78 張、52.4 MiB**。
- Recipe 儲存的 OK／NG 路徑是 `TrainingImageCache/R002/1/OK` 這類**相對路徑**，不用來源 D 槽就能離線預覽。公司端須複製整份 `App-net472-Slim/`。
- 目前總部署包（程式 + 快取）約 **152.5 MiB**。未複製 770 張全量影像，也未複製原廠 AI 模型。
- 第 5 站缺 `R002-CCD5`，故不建立假目錄或假基準圖；可切站但顯示「未設定」。
- 首頁卡片若該站沒有已校準 `pattern.jpg`，只取 OK 圖庫中一張作**樣本預覽**，並標明「未校準」，不當作 AI 定位或光衰判定模型。

## 原廠 Recipe 與 AI 檔案索引

- 真實原廠 Recipe：`Recipe/3923-R002/Setting.ini`、`station1.ini` ～ `station8.ini`。
- `station1.ini` 的 `LocateSearchRect_X/Y/W/H` 為 `169/113/251/240`，`UseUNET=True`；這是**原廠定位參數**，不等於我們現有的亮度測量 ROI。
- `Setting.ini` 的 `AIStation_1～8=0`、`EnableRemoteAI=0`；各站 `AIRoot_Path` 欄位為空。不能以 INI 中缺路徑推論沒有模型。
- 實體模型集中在 `Model/R002-CCD1.h5d` 和 `Model/R002-CCD1.pbd`，CCD2／3／4／6／7／8 同樣有基本成對檔案（另有不同日期／DW 變體）。
- Recipe `vendorRecipePath` 及各站 `vendorAiModelFiles` 保存了來源路徑以便研究，**並未移植原廠訓練引擎／UNet 解譯器**，不會以該模型參與現有影像判定。

## 實際驗收

- .NET Framework 4.7.2 Release Build 0 errors（132 warnings）。
- 以指定來源啟動 `LumasharpChecker.exe --import-trainingimage=<來源站別路徑>`：成功建立唯一 `R002（TrainingImage）` Recipe。
- UI 八站總覽看到 R002、7 個 OK 可用站、7 個原廠 AI 模型索引；點第 2 站進單站頁，OK 圖庫顯示 **圖片 1 / 5**，點 **NG DB** 改為 **圖片 1 / 6**，再點 **OK DB** 回到 **圖片 1 / 5**。
- 第 5 站圖片顯示 **0 / 0**，並提示該站圖庫不存在，不誤報。
- 將程序關閉並**不帶匯入參數重新啟動**：R002 Recipe、圖庫狀態及七站模型索引恢復。
- 使用不相同的啟動工作目錄（`C:\Windows\System32`）仍正常開啟：App 啟動時已把工作目錄切到自身目錄，供 Recipe／快取以相對路徑存取。
- 重複執行同一個 R002 匯入：Recipe 數量仍是 1、ID 不變、78 張快取不重複建立；原版 Git 仍乾淨。

## 再匯入及下一階段

1. 在首頁按 **TrainingImage 匯入**，選取原廠 `R002-CCD1` 或其 `Origional/1` 子目錄。再次匯入同名資料會更新原 Recipe，不另建第二份。
2. 選擇原廠其它產品如 `<產品>-CCD1` 也可以依同規則建立相應 Recipe。
3. 這只是離線圖片可攜快取。正式測試光衰判定前，要對照原廠的定位 ROI、確認每站基準圖／亮度與銳利度閾值，實際完成並保存檢測 Profile，再以同圖比對原版與 net472 演算法。
4. `Model/*.h5d`、`*.pbd` 的格式、模型前處理與輸入輸出仍待解析；此輪不將它們當成可直接使用的 `pattern.jpg`。
