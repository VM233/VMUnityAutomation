[CmdletBinding()]
param(
    [switch]$Check,
    [string]$PackageRoot = (Join-Path $PSScriptRoot '..')
)

$ErrorActionPreference = 'Stop'
$packageId = 'com.vm233.unity-automation'
$packageRoot = [IO.Path]::GetFullPath($PackageRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
$utf8 = [Text.UTF8Encoding]::new($false)
$sha256 = [Security.Cryptography.SHA256]::Create()

try {
    $manifest = Get-Content -LiteralPath (Join-Path $packageRoot 'package.json') -Raw | ConvertFrom-Json
    if ($manifest.name -ne $packageId) { throw 'The explicit root is not the VM Unity Automation package.' }
    $inside = & git -C $packageRoot rev-parse --is-inside-work-tree 2>$null
    if ($LASTEXITCODE -ne 0 -or $inside -ne 'true') { throw 'Package identity publication requires its Git worktree.' }
    $gitRoot = & git -C $packageRoot rev-parse --show-toplevel
    if ([IO.Path]::GetFullPath($gitRoot).TrimEnd([IO.Path]::DirectorySeparatorChar) -ne $packageRoot) {
        throw 'PackageRoot must be the Git worktree root, not a nested package or external directory.'
    }
    $publishedByPath = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    $publishedGuids = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    & git -C $packageRoot rev-parse --verify HEAD *> $null
    if ($LASTEXITCODE -eq 0) {
        $publishedRecords = @(& git -c core.quotePath=false -C $packageRoot grep -n -E '^guid:[[:space:]]*[0-9a-fA-F]{32}[[:space:]]*$' HEAD -- '*.meta')
        if ($LASTEXITCODE -gt 1) { throw 'Could not read committed package identities.' }
        if ($publishedRecords.Count -gt 2048) { throw 'Published metadata exceeds the 2048-record budget.' }
        foreach ($record in $publishedRecords) {
            if ($record -notmatch '^HEAD:(.+\.meta):\d+:guid:\s*([0-9a-fA-F]{32})\s*$') { throw 'Invalid committed metadata identity record.' }
            $path = $Matches[1]
            $guid = $Matches[2].ToLowerInvariant()
            $publishedByPath.Add($path, $guid)
            if (!$publishedGuids.Add($guid)) { throw "Duplicate published GUID '$guid'." }
        }
    }
    $metaFiles = @(Get-ChildItem -LiteralPath $packageRoot -Recurse -File -Filter '*.meta' | Sort-Object FullName)
    if ($metaFiles.Count -gt 2048) { throw 'Working metadata exceeds the 2048-record budget.' }
    $replacements = [ordered]@{}
    $currentGuids = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $expectedGuids = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $preserved = 0
    foreach ($metaFile in $metaFiles) {
        if ($metaFile.Length -gt 64KB) { throw 'A metadata record exceeds 64 KiB.' }
        $relativeMetaPath = $metaFile.FullName.Substring($packageRoot.Length + 1).Replace('\', '/')
        $text = [IO.File]::ReadAllText($metaFile.FullName)
        $match = [Text.RegularExpressions.Regex]::Match($text, '(?m)^guid:\s*([0-9a-fA-F]{32})\s*$')
        if (!$match.Success) { throw "Meta file '$relativeMetaPath' has no Unity GUID." }
        $currentGuid = $match.Groups[1].Value.ToLowerInvariant()
        if (!$currentGuids.Add($currentGuid)) { throw "Duplicate working GUID '$currentGuid'." }
        if ($publishedByPath.ContainsKey($relativeMetaPath)) {
            $expectedGuid = $publishedByPath[$relativeMetaPath]
            if ($currentGuid -ne $expectedGuid) { throw "Published asset '$relativeMetaPath' changed identity. Preserve its GUID and migrate references through the actual asset owner." }
            $preserved++
        }
        elseif ($publishedGuids.Contains($currentGuid)) {
            $expectedGuid = $currentGuid
            $preserved++
        }
        else {
            $assetPath = $relativeMetaPath.Substring(0, $relativeMetaPath.Length - 5)
            $identity = "$packageId" + [char]10 + $assetPath
            $hash = $sha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($identity))
            $expectedGuid = ([BitConverter]::ToString($hash)).Replace('-', '').ToLowerInvariant().Substring(0, 32)
        }
        if (!$expectedGuids.Add($expectedGuid)) { throw "Expected GUID collision for '$relativeMetaPath'." }
        if ($currentGuid -ne $expectedGuid) { $replacements[$currentGuid] = $expectedGuid }
    }
    if ($Check) {
        if ($replacements.Count -ne 0) { throw "$($replacements.Count) new package GUIDs do not match their deterministic owner." }
        [ordered]@{ok=$true;packageId=$packageId;metaCount=$metaFiles.Count;preservedPublishedIdentities=$preserved;changedFileCount=0} | ConvertTo-Json -Compress
        exit 0
    }
    $textExtensions = @('.meta','.cs','.md','.json','.uxml','.uss','.asset','.prefab','.unity','.asmdef','.asmref','.txt','.xml','.yml','.yaml','.py','.ps1','.shader','.hlsl','.cginc','.compute','.sh')
    $textFiles = @(Get-ChildItem -LiteralPath $packageRoot -Recurse -File | Where-Object { $_.FullName -notlike "$packageRoot\.git\*" -and $textExtensions -contains $_.Extension.ToLowerInvariant() })
    if ($textFiles.Count -gt 8192) { throw 'Reference publication exceeds 8192 text files.' }
    $totalBytes = 0L
    foreach ($file in $textFiles) {
        $totalBytes += $file.Length
        if ($file.Length -gt 4MB -or $totalBytes -gt 64MB) { throw 'Reference publication exceeds its text byte budget.' }
    }
    $changedFileCount = 0
    $guidToken = [Text.RegularExpressions.Regex]::new('(?i)(?<![0-9a-f])[0-9a-f]{32}(?![0-9a-f])')
    foreach ($file in $textFiles) {
        $text = [IO.File]::ReadAllText($file.FullName)
        $updated = $guidToken.Replace($text, {
            param($match)
            $identity = $match.Value.ToLowerInvariant()
            if ($replacements.Contains($identity)) { return $replacements[$identity] }
            return $match.Value
        })
        if ($updated -ne $text) {
            [IO.File]::WriteAllText($file.FullName, $updated, $utf8)
            $changedFileCount++
        }
    }
    [ordered]@{ok=$true;packageId=$packageId;metaCount=$metaFiles.Count;preservedPublishedIdentities=$preserved;changedFileCount=$changedFileCount} | ConvertTo-Json -Compress
}
finally { $sha256.Dispose() }
