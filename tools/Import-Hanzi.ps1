param(
    [Parameter(Mandatory=$true)][string]$Characters
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sourceRoot = Join-Path $projectRoot 'external\makemeahanzi'
$destination = Join-Path $projectRoot 'assets\hanzi'
$wanted = @($Characters.ToCharArray() | ForEach-Object { [string]$_ } | Where-Object { -not [char]::IsWhiteSpace($_[0]) } | Select-Object -Unique)
foreach ($word in $wanted) {
    if ($word -notmatch '^\p{IsCJKUnifiedIdeographs}$') { throw "Unsupported character: $word" }
}
if ($wanted.Count -eq 0) { throw 'Provide at least one Chinese character.' }
$dictionary = @{}
$graphics = @{}
# Filter before JSON parsing: only the requested records need conversion.
foreach ($kind in @('dictionary', 'graphics')) {
    foreach ($line in [IO.File]::ReadLines((Join-Path $sourceRoot ($kind + '.txt')))) {
        if ($line -match '^\{"character":"([^"]+)"' -and $wanted -contains $Matches[1]) {
            $entry = $line | ConvertFrom-Json
            if ($kind -eq 'dictionary') { $dictionary[$entry.character] = $entry }
            else { $graphics[$entry.character] = $entry }
        }
    }
}
$revision = 'bddc96d41bef78427ed0e034e9f7e31d71fd1b92'
if (Test-Path -LiteralPath (Join-Path $sourceRoot '.git')) {
    $revision = & git -C $sourceRoot rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read source revision.' }
}
$entries = @()
foreach ($word in $wanted) {
    if (-not $dictionary.ContainsKey($word) -or -not $graphics.ContainsKey($word)) { throw "Character absent from source: $word" }
    $g = $graphics[$word]
    $d = $dictionary[$word]
    if ($g.strokes.Count -ne $g.medians.Count -or $d.matches.Count -ne $g.medians.Count) { throw "Inconsistent stroke data: $word" }
    foreach ($median in $g.medians) { if ($median.Count -lt 2) { throw "Incomplete median: $word" } }
    $entries += [ordered]@{
        character = $word
        strokes = $g.strokes
        medians = $g.medians
        decomposition = $d.decomposition
        matches = $d.matches
        source = 'https://github.com/skishore/makemeahanzi'
        revision = $revision
        strokeOrderStandard = 'upstream-PRC'
        taiwanStrokeOrderVerified = $false
    }
}
[IO.Directory]::CreateDirectory($destination) | Out-Null
foreach ($entry in $entries) {
    [IO.File]::WriteAllText((Join-Path $destination ($entry.character + '.json')), ($entry | ConvertTo-Json -Depth 30), (New-Object Text.UTF8Encoding($false)))
    Write-Output ("Imported {0}: {1} strokes, {2}" -f $entry.character, $entry.medians.Count, $entry.decomposition)
}
