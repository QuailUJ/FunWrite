# FunWrite 2.1

FunWrite 是離線中文書寫練習程式。可選擇練習字、示範筆順，並以寫字板取得的筆跡提供字形分數與書寫回饋。

## 下載與開啟

1. 到 [2.1 發行頁](https://github.com/QuailUJ/FunWrite/releases/tag/v2.1.0) 下載 **`FunWrite2.1-Portable-20261113.zip`**。
2. 將 ZIP 完整解壓縮到可寫入的資料夾，保留同資料夾內的 DLL 與字庫。
3. 點兩下 `FunWrite2.1.exe`。執行環境為 Windows x86／x64 與 .NET Framework 4.8 或 4.8.1。

開啟後即可查看範例與示範筆順。實際書寫與評分需要原專案支援的 DigiNote 寫字板，並完成原廠裝置設定。

## 操作方式

1. 按右上角「練習字與位置」，或按 **F2**，選擇 1～12 個字。上、下各六個位置，依序對應寫字板。
2. 貼上「山 水 火」等字串，按「依輸入排列」。若字庫缺少其中的字，可先按「匯入字庫」，再排列。按「儲存並套用」後，重開會保留設定。
3. 點範例查看筆順，在寫字板對應字格書寫，完成後按「評分」或 **Enter**。可按「重播」看筆跡，或查看字形比對及詳細回饋。
4. 按 **Ctrl＋O** 讀取已存的筆跡。程式把練習筆跡保存在執行資料夾內的 `records`。

「還原本次編輯」可在儲存前回到開啟設定時的字數與順序。匯入使用隨附的離線字源，每批最多 12 字，支援字源有資料且不超過 16 筆的中文字。匯入字的筆順尚未逐字對照台灣標準。

## 評分方式

程式先以整字等比例縮放比對筆畫，再視字庫資料檢查部件的比例、間距與對齊。**通過需要至少 60 分，且筆數、筆順、方向與整筆軌跡均通過檢查。** 筆數不符最高 59 分。詳細規則與兩版差異見 [評分比較](docs/version-comparison.html)，核心呼叫順序見 [流程圖](docs/program-flow.png)。

## 驗證與建置

- 2026/11/13 在本機確認 ZIP 解壓後可啟動，核對包內 55 個檔案的 SHA-256，並測試離線匯入、貼字排列、還原與設定快捷鍵。
- 原專案十套測試通過。94 筆既有筆跡的通過判定一致。實體寫字板及另一台電腦的完整操作仍待驗收。
- `src` 為 C# WinForms 原始碼，`assets` 為練習字庫，`external/makemeahanzi` 包含離線字源與授權。使用 Visual Studio 2022 與 .NET Framework 4.8 targeting pack，開啟 `FunWrite2.0.sln` 建置 2.1，或在 PowerShell 執行 `./tools/Build.ps1`。
- 公開版本的 `./tools/Run-ScoringChecks.ps1` 會執行可獨立運行的測試。需使用原機私人筆跡的回歸測試由原機保留資料執行。

字源來自 [Make Me a Hanzi](https://github.com/skishore/makemeahanzi)。授權原件見 [COPYING](external/makemeahanzi/COPYING)。
