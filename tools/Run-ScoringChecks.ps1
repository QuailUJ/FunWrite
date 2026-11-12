param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    if (-not $SkipBuild) { & "$PSScriptRoot\Build.ps1" -Configuration Debug }
    New-Item -ItemType Directory -Path tests\bin,tests\output -Force | Out-Null
    Copy-Item -LiteralPath app\FunWrite2.1.exe,app\DigiNoteSample.exe -Destination tests\bin
    $compiler='C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
    $privateTests = @('GoldenScoreTests','ScoreTests','StrokeAccuracyTests','StructureTests')
    $hasPrivateFixtures = (Test-Path -LiteralPath 'tests/fixtures/scores-before-refactor.csv') -and (Test-Path -LiteralPath 'records/20261021_144020_883_一_0.txt')
    foreach ($test in Get-ChildItem tests -Filter '*.cs') {
        if (-not $hasPrivateFixtures -and $test.BaseName -in $privateTests) {
            Write-Output "SKIP: $($test.BaseName) requires private recording fixtures."
            continue
        }
        & $compiler /nologo /platform:x86 /r:app\FunWrite2.1.exe /r:System.Drawing.dll /r:System.Windows.Forms.dll "/out:tests\bin\$($test.BaseName).exe" $test.FullName
        if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $($test.Name)" }
        [string[]]$testArguments=@(if($test.BaseName -eq 'UiTests'){'--preview'}elseif($test.BaseName -in @('ScoreTests','TrajectoryTests')){'assets\templates'})
        & ".\tests\bin\$($test.BaseName).exe" @testArguments *> "tests\output\$($test.BaseName).log"
        if ($LASTEXITCODE -ne 0) { Get-Content "tests\output\$($test.BaseName).log"; throw "Failed: $($test.BaseName)" }
        Write-Output "PASS: $($test.BaseName)"
    }
} finally { Pop-Location }
