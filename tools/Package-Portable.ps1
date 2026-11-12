param([string]$Name='FunWrite2.1-Portable')
$ErrorActionPreference='Stop'
if ($Name -notmatch '^[A-Za-z0-9._-]+$') { throw 'Invalid package name.' }
$projectRoot=Split-Path $PSScriptRoot -Parent
$dist=Join-Path $projectRoot 'dist'
$package=Join-Path $dist $Name
$zip=Join-Path $dist ($Name+'.zip')
if ((Test-Path -LiteralPath $package) -or (Test-Path -LiteralPath $zip)) { throw 'Package already exists. Choose a new name to preserve it.' }
& "$PSScriptRoot\Build.ps1" -Configuration Release
New-Item -ItemType Directory -Path $package,(Join-Path $package 'records'),(Join-Path $package 'docs'),(Join-Path $package 'licenses') -Force | Out-Null
foreach ($file in @('FunWrite2.1.exe','FunWrite2.1.exe.config','DigiNoteSample.exe','DigitNoteController.dll','DigitNoteUSBController.dll','PCOMM.DLL')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot ('app\'+$file)) -Destination $package
}
$packagedAssets = Join-Path $package 'assets'
New-Item -ItemType Directory -Path $packagedAssets -Force | Out-Null
foreach ($entry in @('practice-words.txt','hanzi','templates','wave')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot ('assets\'+$entry)) -Destination $packagedAssets -Recurse
}
$offline=Join-Path $package 'external\makemeahanzi'
New-Item -ItemType Directory -Path $offline -Force | Out-Null
foreach ($file in @('dictionary.txt','graphics.txt','COPYING','LGPL','APL')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot ('external\makemeahanzi\'+$file)) -Destination $offline -Recurse
}
foreach ($file in @('version-comparison.html','program-flow.png','program-flow.svg')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot ('docs\'+$file)) -Destination (Join-Path $package 'docs')
}
$upstream=Join-Path $projectRoot 'external\makemeahanzi'
foreach ($file in @('COPYING','LGPL','APL')) { Copy-Item -LiteralPath (Join-Path $upstream $file) -Destination (Join-Path $package 'licenses') -Recurse }
@'
FunWrite 2.1 可攜版

1. 將整個資料夾解壓縮到可寫入的位置，例如 D:\FunWrite2.1。
2. 直接點兩下這一層的 FunWrite2.1.exe。不需 Visual Studio、SQL、帳號或網路。
3. 使用寫字板前，先完成原廠裝置配對／驅動設定；程式內可重新連接。
4. 不連寫字板也可開啟畫面、看範例與筆順；滑鼠不能取代寫字板寫字。
5. 筆跡會存入 records。這個發行包不包含原電腦的私人筆跡紀錄。
6. 請保留 EXE、DLL 和 assets，不能只複製主程式。不要直接在 ZIP 裡開 EXE。
7. 操作：點字示範。Enter 評分、Delete 重寫、Esc 離開、Ctrl＋O 讀取舊筆跡。
8. 評分標準與 1.0 比較：用瀏覽器開 docs\version-comparison.html。
9. 按 F2 開啟「練習字與位置」。可貼上「山 水 火」再按「依輸入排列」。
   缺字時先按「匯入字庫」。排錯可按「還原本次編輯」。確認後按「儲存並套用」。

環境：Windows x86/x64，.NET Framework 4.8 或相容的 4.8.1。
主程式是 x86，配合原本 32 位元寫字板 SDK。未驗證 Windows ARM／macOS／Linux。
若系統提示缺少 .NET Framework，需先安裝 4.8：本包未包含 .NET 安裝程式，
所以沒有相容 Framework 的電腦無法僅解壓縮就執行。
若可開畫面但裝置未連線，請檢查原廠驅動、USB／藍牙配對與裝置是否被其他程式占用。
包內已附應用程式端 SDK DLL，但目前專案沒有原廠驅動安裝包可供附上。
完整離線計算，不呼叫 OCR 或線上服務。

寫字板連線與實寫需要用目標電腦及裝置驗收。
'@ | Set-Content -LiteralPath (Join-Path $package '開始使用.txt') -Encoding UTF8
@'
Make Me a Hanzi
Upstream: https://github.com/skishore/makemeahanzi
Source revision: bddc96d41bef78427ed0e034e9f7e31d71fd1b92
Included derived data: assets/hanzi/*.json, retaining outlines,
medians, decomposition, matches and source metadata. Coordinate/metadata conversion
is by FunWrite import tooling. Taiwan stroke order is not yet verified.
License originals: licenses/COPYING, licenses/LGPL, licenses/APL/*.
The full downloaded upstream is retained in the development project external/ folder.
Vendor DigiNote SDK files are original project binaries; their license ownership
is unchanged. No separate vendor driver installer is available in this project.
'@ | Set-Content -LiteralPath (Join-Path $package 'licenses\SOURCES.txt') -Encoding UTF8
Get-ChildItem -LiteralPath $package -Recurse -File | Where-Object Name -ne 'SHA256.csv' | Sort-Object FullName | ForEach-Object {
    [pscustomobject]@{Path=$_.FullName.Substring($package.Length+1);SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
} | Export-Csv -LiteralPath (Join-Path $package 'SHA256.csv') -NoTypeInformation -Encoding UTF8
Compress-Archive -LiteralPath $package -DestinationPath $zip -CompressionLevel Optimal
Get-Item -LiteralPath $zip | Select-Object FullName,Length
