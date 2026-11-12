param([ValidateSet('Debug','Release')][string]$Configuration='Release')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$buildTool='C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path -LiteralPath $buildTool)) { throw 'Requires Visual Studio 2022 MSBuild and .NET Framework 4.8 targeting pack.' }
Push-Location $projectRoot
try {
    New-Item -ItemType Directory -Path tests\output -Force | Out-Null
    & $buildTool FunWrite2.0.sln /t:Build "/p:Configuration=$Configuration" '/p:Platform=Any CPU' /v:quiet /nologo *> "tests\output\build-$Configuration.log"
    if ($LASTEXITCODE -ne 0) { Get-Content "tests\output\build-$Configuration.log"; throw 'Build failed. Close the running FunWrite application before rebuilding.' }
    Write-Output 'Build succeeded: app\FunWrite2.1.exe'
} finally { Pop-Location }
