# 流程耗時量測

獨立 net472 x64 主控台，透過反射執行同一 NativeRecipeBridge 與光源 helper。暖機 10 次後取 50 次中位數，不啟動 WPF，不寫正式檢測報告／配方。包含 native worker 自身檢測暫存，finally 關閉自身 worker 並清掉自身 session。

參數依序：主程式 EXE、App 根目錄、recipe.json、輸入圖、light-inspection.json、站號、輸出 JSON。配方已存在且隔離副本一致時只讀配方；勿以尚未建立隔離副本的正式配方做基準。

各欄為自己的中位數，故不要求相加恰等於總耗時。含完整配方校驗／native 往返／光源量測，不含 WPF 預覽綁定及正式報告輸出。測量時使用 --artifacts-path 指向專用暫存目錄，避免 diagnostics 留 bin/obj。

另可用「主程式 EXE、App 根目錄、hash-check」三個參數檢查 SHA256 revision 一致性與同長度同時間戳竄改；fixture 產生於受測 EXE 旁，應對暫存發布成品執行。
