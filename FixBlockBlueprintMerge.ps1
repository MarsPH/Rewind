# Run from the Unity project root (the folder containing Assets).
# Repairs only the three conflicts found in the supplied BlockBlueprint SDF asset.
param(
    [string] $ProjectRoot = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'
$assetPath = Join-Path $ProjectRoot 'Assets\UI\Fonts\blockblueprint\BlockBlueprint SDF.asset'
if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
    throw "Asset not found: $assetPath. Run this script from the Unity project root or pass -ProjectRoot."
}

$text = [System.IO.File]::ReadAllText($assetPath)
$pattern = '(?ms)^<<<<<<< HEAD\r?\n(?<ours>.*?)^=======\r?\n(?<theirs>.*?)^>>>>>>>[^\r\n]*(?:\r?\n|$)'
$conflicts = [regex]::Matches($text, $pattern)
if ($conflicts.Count -ne 3) {
    throw "Expected exactly 3 known merge conflicts; found $($conflicts.Count). No changes made."
}

$expected = @('  m_GlyphTable:', '  m_UsedGlyphRects:', '  image data: 1048576')
for ($i = 0; $i -lt 3; $i++) {
    if (-not $conflicts[$i].Groups['theirs'].Value.StartsWith($expected[$i])) {
        throw "Conflict $($i + 1) differs from the supplied file. No changes made."
    }
}

$resolved = [regex]::Replace(
    $text,
    $pattern,
    [System.Text.RegularExpressions.MatchEvaluator] {
        param($match)
        return $match.Groups['theirs'].Value
    }
)

# The original file's nonconflicting Texture2D header still describes HEAD's empty
# 1x1 atlas. Make it consistent with the incoming populated 1024x1024 Alpha8 atlas.
foreach ($field in @('m_Width', 'm_Height', 'm_CompleteImageSize')) {
    $fieldPattern = '(?m)^  ' + $field + ': 1(?=\r?$)'
    if ([regex]::Matches($resolved, $fieldPattern).Count -ne 1) {
        throw "Unexpected $field value. No changes made."
    }
    $newValue = if ($field -eq 'm_CompleteImageSize') { '1048576' } else { '1024' }
    $resolved = [regex]::Replace($resolved, $fieldPattern, '  ' + $field + ': ' + $newValue)
}

if ([regex]::IsMatch($resolved, '(?m)^(<<<<<<<|=======|>>>>>>>)')) {
    throw 'Unresolved conflict markers remain. No changes made.'
}
$atlas = [regex]::Match($resolved, '(?m)^  _typelessdata: ([0-9a-fA-F]+)\r?$')
if (-not $atlas.Success -or $atlas.Groups[1].Length -ne 2097152) {
    throw 'Incoming atlas data is not the expected 1,048,576 bytes. No changes made.'
}

$backupPath = Join-Path $ProjectRoot 'BlockBlueprint SDF.asset.before-merge-fix.bak'
if (Test-Path -LiteralPath $backupPath) {
    throw "Backup already exists: $backupPath. Move it before rerunning; no changes made."
}
[System.IO.File]::Copy($assetPath, $backupPath)
[System.IO.File]::WriteAllText($assetPath, $resolved, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Fixed: $assetPath"
Write-Host "Backup: $backupPath"
Write-Host 'All 3 conflicts resolved; populated glyph tables and 1024x1024 atlas preserved.'
