<#
.SYNOPSIS
  Mirrors Adnd.Core/Adnd.Data source (.cs) and Adnd.Data's JSON reference data into the
  WizardryViewer Unity project, so the Unity copy can be re-synced after upstream changes
  without hand-editing.

.NOTES
  - Adnd.Core/Adnd.Data are plain net10.0 class libraries with no WinForms dependency (see
    Adnd.Game.csproj, which references them but not vice versa) - that split is what makes
    embedding them in Unity possible without dragging in WinForms.
  - .cs files land under Assets/Plugins/AdndGame/{Core,Data} and compile into Assembly-CSharp
    like everything else in this project (no asmdef exists anywhere in WizardryViewer yet).
  - JSON reference data (Items/Monsters/Spells/Treasure/Encounters) lands under
    Assets/StreamingAssets/Data/... matching the repositories' own "Data/<Folder>" convention,
    so pointing a repository at Application.streamingAssetsPath + "/Data/Items" etc. just works
    on Windows Standalone/Quest (real filesystem). Android/iOS StreamingAssets is NOT a normal
    listable/writable folder - that rework is tracked separately, not solved by this script.
  - Only files this script would itself have written are ever deleted (matched by relative
    path against the current source tree), so removing a file upstream removes it here too,
    without touching anything else under Assets/.
  - Unity 6000.4.11f1's bundled Roslyn compiles at most C# 11 even with Assets/csc.rsp set to
    -langversion:latest, so C# 12 collection expressions ([1, 2, 3]) fail with CS1525. .cs files
    get a conservative text transform on the way in: "[<literal list>]" in value position
    (immediately after =, comma, or open-paren, ignoring whitespace) becomes "new[] { <literal
    list> }", which is semantically identical, just older syntax. Only pure literal lists
    (digits/quotes/.,-;/| and whitespace, no nested brackets or identifiers) are touched, so an
    indexer like Classes[0] or an indexer-initializer like [CharacterClass.Fighter] = ... is
    never matched (the character right before '[' isn't one of =,( ). After transforming, the
    script re-scans for anything that still looks like an unconverted collection expression and
    throws with file/line detail rather than shipping a guess - Robert's repo keeps changing, so
    this needs to fail loudly on a pattern it doesn't recognize, not silently miscompile.
  - System.Text.Json (used throughout Adnd.Data's repositories + a couple of Adnd.Core files)
    isn't in Unity's class library at all (CS0234) and there's no Unity-hosted package for it.
    Rather than vendor raw NuGet DLLs of unverified IL2CPP/AOT compatibility onto the actual
    Quest/Android/iOS targets this project exists for, the files that touch it were hand-ported
    to Newtonsoft.Json (already a dependency here - see Packages/manifest.json - and Unity's own
    long-proven AOT-safe JSON library). That porting is semantic, not a safe mechanical text
    transform (PartyMembersConverter.cs's converter API shape genuinely differs between the two
    libraries), so it is NOT redone by this script. $NewtonsoftForkedRelPaths below lists exactly
    which files are hand-maintained forks from this point on - this script skips re-copying them
    from source, so upstream changes to those specific files will NOT reach Unity automatically.
    If Robert's repo changes one of them, re-port it by hand and update this comment/list.
  - StringSplitOptions.TrimEntries (.NET 5+) doesn't exist on Unity's older corelib either
    (CS0117). Both current call sites already follow the split with an explicit ".Select(t =>
    t.Trim())" (ParseTreasureTypes) or split already-whitespace-stripped input (RollAmount, which
    does Replace(" ", "") first) - the flag is redundant in both, so stripping it from an "X |
    StringSplitOptions.TrimEntries" or "StringSplitOptions.TrimEntries | X" combination is
    behavior-neutral. If it ever appears alone (not OR'd with another flag), the script throws
    rather than guess, since dropping it outright there WOULD change behavior.
  - Random.Shared (.NET 6+) doesn't exist on Unity's older corelib either (CS0117), and unlike
    DistinctBy it's a STATIC property, so it can't be polyfilled as an extension method - there's
    no language mechanism to add a static member to an existing sealed class. Instead the literal
    token "Random.Shared" is text-substituted to the fully qualified Adnd.Unity.Compat.SharedRandom
    .Instance (see SharedRandom.cs), a single shared System.Random - safe here because this
    codebase's game logic runs single-threaded already (WinForms UI thread today, Unity main
    thread here), same assumption the rest of Adnd.Core/Adnd.Data makes. This IS safe to automate
    (unlike the JSON port): the token is unambiguous and the substitution is a pure rename with
    identical semantics, not a behavior change.
  - manifest.json (written alongside the synced JSON, at Assets/StreamingAssets/Data/manifest.json)
    lists every relative JSON path plus a content-hash "dataVersion". There is no API to list an
    Android APK's contents at runtime (Directory.GetFiles doesn't work inside it), so
    AdndDataBootstrap.cs (Unity-only, not synced) reads this manifest via UnityWebRequest first to
    know what to extract, then fetches each listed file the same way. dataVersion lets it skip
    re-extraction on a launch where nothing changed.
#>

param(
    [string]$RepoRoot = $PSScriptRoot,
    [string]$UnityProject = (Join-Path $PSScriptRoot 'WizardryViewer')
)

$NewtonsoftForkedRelPaths = @{
    'Core' = @(
        'Characters\Character.cs',
        'Config\GameRulesProvider.cs'
    )
    'Data' = @(
        'Characters\CharacterRepository.cs',
        'Encounters\EncounterRepository.cs',
        'Items\ItemRepository.cs',
        'Monsters\MonsterRepository.cs',
        'Party\Party.cs',
        'Party\PartyMembersConverter.cs',
        'Party\PartyRepository.cs',
        'Spells\SpellRepository.cs',
        'Treasure\TreasureTableRepository.cs'
    )
}

$collectionExprPattern = "(?<=[=,(]\s*)\[([\d,\s'`"\.\-;/|]+)\]"
$residualSuspectPattern = "(?<=[=,(]\s*)\[[\d'`"]"
$randomSharedPattern = "\bRandom\.Shared\b"
$randomSharedReplacement = 'Adnd.Unity.Compat.SharedRandom.Instance'
$trimEntriesTrailingPattern = "\s*\|\s*StringSplitOptions\.TrimEntries"
$trimEntriesLeadingPattern = "StringSplitOptions\.TrimEntries\s*\|\s*"

function Convert-NetApiGaps {
    param([string]$Text, [string]$RelPath)

    $converted = [regex]::Replace($Text, $collectionExprPattern, 'new[] { $1 }')
    $converted = [regex]::Replace($converted, $randomSharedPattern, $randomSharedReplacement)
    $converted = [regex]::Replace($converted, $trimEntriesTrailingPattern, '')
    $converted = [regex]::Replace($converted, $trimEntriesLeadingPattern, '')

    if ($converted -match "StringSplitOptions\.TrimEntries") {
        throw "StringSplitOptions.TrimEntries survived in $RelPath used in a shape this script doesn't recognize (not OR'd with another flag) - handle it by hand, dropping it here would silently change behavior."
    }

    $residual = [regex]::Matches($converted, $residualSuspectPattern)
    if ($residual.Count -gt 0) {
        $lines = $converted -split "`n"
        $details = foreach ($m in $residual) {
            $lineNo = ($converted.Substring(0, $m.Index) -split "`n").Count
            "    line $lineNo`: $($lines[$lineNo - 1].Trim())"
        }
        throw "Unconverted collection-expression-like syntax survived in $RelPath - fix the pattern before syncing:`n$($details -join "`n")"
    }

    return $converted
}

function Sync-Mirror {
    param(
        [string]$SourceDir,
        [string]$DestDir,
        [string]$Filter,
        [switch]$TransformNetApiGaps,
        [string[]]$ExcludeRelPaths = @()
    )

    if (-not (Test-Path $SourceDir)) {
        throw "Source not found: $SourceDir"
    }

    New-Item -ItemType Directory -Force -Path $DestDir | Out-Null

    $sourceRoot = (Resolve-Path $SourceDir).Path.TrimEnd('\')
    $destRoot = (Resolve-Path $DestDir).Path.TrimEnd('\')
    $excludeSet = @{}
    foreach ($p in $ExcludeRelPaths) { $excludeSet[$p] = $true }

    $sourceFiles = Get-ChildItem -Path $sourceRoot -Filter $Filter -Recurse -File |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }

    $sourceRel = @{}
    $transformedCount = 0
    $skippedForked = 0
    foreach ($f in $sourceFiles) {
        $rel = $f.FullName.Substring($sourceRoot.Length).TrimStart('\')
        $sourceRel[$rel] = $true

        if ($excludeSet.ContainsKey($rel)) {
            $skippedForked++
            continue
        }

        $destPath = Join-Path $destRoot $rel
        $destDirPath = Split-Path $destPath
        if (-not (Test-Path $destDirPath)) {
            New-Item -ItemType Directory -Force -Path $destDirPath | Out-Null
        }

        if ($TransformNetApiGaps) {
            $original = Get-Content -Path $f.FullName -Raw
            $converted = Convert-NetApiGaps -Text $original -RelPath $rel
            if ($converted -ne $original) {
                $transformedCount++
            }
            Set-Content -Path $destPath -Value $converted -NoNewline
        } else {
            Copy-Item -Path $f.FullName -Destination $destPath -Force
        }
    }

    $removed = 0
    if (Test-Path $destRoot) {
        $destFiles = Get-ChildItem -Path $destRoot -Filter $Filter -Recurse -File
        foreach ($f in $destFiles) {
            $rel = $f.FullName.Substring($destRoot.Length).TrimStart('\')
            if (-not $sourceRel.ContainsKey($rel)) {
                Remove-Item -Force $f.FullName
                $meta = "$($f.FullName).meta"
                if (Test-Path $meta) { Remove-Item -Force $meta }
                Write-Host "  removed stale: $rel"
                $removed++
            }
        }
    }

    return [PSCustomObject]@{ Copied = $sourceFiles.Count - $skippedForked; Removed = $removed; Transformed = $transformedCount; SkippedForked = $skippedForked }
}

Write-Host "Syncing Adnd.Core -> Assets/Plugins/AdndGame/Core"
$core = Sync-Mirror -SourceDir (Join-Path $RepoRoot 'Adnd.Core') -DestDir (Join-Path $UnityProject 'Assets\Plugins\AdndGame\Core') -Filter '*.cs' -TransformNetApiGaps -ExcludeRelPaths $NewtonsoftForkedRelPaths['Core']
Write-Host "  $($core.Copied) files copied, $($core.Removed) stale removed, $($core.Transformed) had collection-expression syntax downgraded, $($core.SkippedForked) left as hand-maintained Newtonsoft forks"

Write-Host "Syncing Adnd.Data -> Assets/Plugins/AdndGame/Data"
$data = Sync-Mirror -SourceDir (Join-Path $RepoRoot 'Adnd.Data') -DestDir (Join-Path $UnityProject 'Assets\Plugins\AdndGame\Data') -Filter '*.cs' -TransformNetApiGaps -ExcludeRelPaths $NewtonsoftForkedRelPaths['Data']
Write-Host "  $($data.Copied) files copied, $($data.Removed) stale removed, $($data.Transformed) had collection-expression syntax downgraded, $($data.SkippedForked) left as hand-maintained Newtonsoft forks"

Write-Host "Syncing Adnd.Data JSON -> Assets/StreamingAssets/Data"
$streamingDataDir = Join-Path $UnityProject 'Assets\StreamingAssets\Data'
$json = Sync-Mirror -SourceDir (Join-Path $RepoRoot 'Adnd.Data') -DestDir $streamingDataDir -Filter '*.json'
Write-Host "  $($json.Copied) files copied, $($json.Removed) stale removed"

Write-Host "Writing manifest.json"
$manifestPath = Join-Path $streamingDataDir 'manifest.json'
$dataFiles = Get-ChildItem -Path $streamingDataDir -Filter '*.json' -Recurse -File |
    Where-Object { $_.Name -ne 'manifest.json' } |
    Sort-Object FullName

$relPaths = @()
$hashInput = New-Object System.Text.StringBuilder
foreach ($f in $dataFiles) {
    $rel = $f.FullName.Substring($streamingDataDir.Length).TrimStart('\').Replace('\', '/')
    $relPaths += $rel
    $fileHash = (Get-FileHash -Path $f.FullName -Algorithm SHA256).Hash
    [void]$hashInput.Append("${rel}:${fileHash};")
}

$sha256 = [System.Security.Cryptography.SHA256]::Create()
$dataVersionBytes = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($hashInput.ToString()))
$dataVersion = [System.BitConverter]::ToString($dataVersionBytes).Replace('-', '').ToLowerInvariant()

$manifest = [PSCustomObject]@{
    dataVersion = $dataVersion
    files = $relPaths
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $manifestPath -NoNewline
Write-Host "  $($relPaths.Count) files listed, dataVersion=$dataVersion"

Write-Host "Done."
