param(
    [Parameter(Mandatory=$true)][string]$ExamplePath,
    [Parameter(Mandatory=$true)][string]$OldHubPath,
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [long]$Offset = 5000000000
)

$ErrorActionPreference = 'Stop'

# fileIDs from the original Hub.unity that we want to carry over.
$keepIds = @(
    1787053895, 1787053896, 1787053897, 1787053898, 1787053899,
    2107663523, 2107663524, 2107663525, 2107663526,
    7464211, 7464212, 7464213, 7464214,
    1428111816, 1428111817,
    1049267636, 1049267637, 1049267638, 1049267639, 1049267640,
    148000270, 148000271, 148000272, 148000273, 148000274,
    151426496, 151426497, 151426498, 151426499, 151426500,
    276561199, 276561200, 276561201, 276561202,
    510650156, 510650157, 510650158, 510650159,
    649626665, 649626666, 649626667, 649626668, 649626669,
    1200570709, 1200570710, 1200570711, 1200570712, 1200570713,
    893874753, 893874754, 893874755, 893874756,
    1471947020, 1471947021, 1471947022, 1471947023, 1471947024,
    1442017170, 1442017171, 1442017172, 1442017173,
    730370876, 730370877, 730370878, 730370879,
    1891419687, 1891419688, 1891419689
)
$keepSet = New-Object 'System.Collections.Generic.HashSet[long]'
foreach ($id in $keepIds) { [void]$keepSet.Add([long]$id) }

$oldContent = [System.IO.File]::ReadAllText($OldHubPath)
$chunks = [System.Text.RegularExpressions.Regex]::Split($oldContent, '(?m)(?=^--- !u!)')

$kept = New-Object System.Text.StringBuilder
foreach ($chunk in $chunks) {
    $m = [System.Text.RegularExpressions.Regex]::Match($chunk, '^--- !u!\d+ &(\d+)')
    if (-not $m.Success) { continue }
    $id = [long]$m.Groups[1].Value
    if ($keepSet.Contains($id)) {
        [void]$kept.Append($chunk)
    }
}
$merged = $kept.ToString()

foreach ($id in $keepIds) {
    $newId = [long]$id + $Offset
    $merged = [System.Text.RegularExpressions.Regex]::Replace($merged, "&${id}(?!\d)", "&${newId}")
    $merged = [System.Text.RegularExpressions.Regex]::Replace($merged, "fileID:\s*${id}(?!\d)", "fileID: ${newId}")
}

# Move Player to a safe spawn position above the demo area on the moon terrain.
$merged = $merged -replace 'm_LocalPosition: \{x: 0, y: 0\.1, z: -2\}', 'm_LocalPosition: {x: 815, y: 140, z: 850}'

# Move Door_PlaneShooter into the demo area in front of the player.
$merged = $merged -replace 'm_LocalPosition: \{x: -2\.6, y: 0, z: 10\}', 'm_LocalPosition: {x: 815, y: 105, z: 870}'

$exampleContent = [System.IO.File]::ReadAllText($ExamplePath)
if (-not $exampleContent.EndsWith("`n")) { $exampleContent = $exampleContent + "`n" }

$final = $exampleContent + $merged

[System.IO.File]::WriteAllText($OutputPath, $final)
Write-Output ("Wrote {0} bytes to {1}" -f $final.Length, $OutputPath)
