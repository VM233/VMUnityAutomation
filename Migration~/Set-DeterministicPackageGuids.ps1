[CmdletBinding()]
param(
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$packageId = 'com.vm233.unity-automation'
$packageRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$utf8 = [Text.UTF8Encoding]::new($false)
$sha256 = [Security.Cryptography.SHA256]::Create()

try {
    $metaFiles = Get-ChildItem -LiteralPath $packageRoot -Recurse -File -Filter '*.meta' |
        Sort-Object FullName
    $replacements = [ordered]@{}
    $expectedGuids = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)

    foreach ($metaFile in $metaFiles) {
        $relativeMetaPath = $metaFile.FullName.Substring($packageRoot.Length + 1).
            Replace('\', '/')
        $assetPath = $relativeMetaPath.Substring(0, $relativeMetaPath.Length - 5)
        $identity = "$packageId`n$assetPath"
        $hash = $sha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($identity))
        $expectedGuid = ([BitConverter]::ToString($hash)).Replace('-', '').
            ToLowerInvariant().Substring(0, 32)
        if (!$expectedGuids.Add($expectedGuid)) {
            throw "Deterministic GUID collision for '$assetPath'."
        }

        $text = [IO.File]::ReadAllText($metaFile.FullName)
        $match = [Text.RegularExpressions.Regex]::Match(
            $text,
            '(?m)^guid:\s*([0-9a-fA-F]{32})\s*$')
        if (!$match.Success) {
            throw "Meta file '$relativeMetaPath' has no Unity GUID."
        }

        $currentGuid = $match.Groups[1].Value.ToLowerInvariant()
        if ($currentGuid -ne $expectedGuid) {
            $replacements[$currentGuid] = $expectedGuid
        }
    }

    if ($Check) {
        if ($replacements.Count -ne 0) {
            throw "$($replacements.Count) package GUIDs do not match the deterministic owner."
        }

        [ordered]@{
            ok = $true
            packageId = $packageId
            metaCount = $metaFiles.Count
            changedFileCount = 0
        } | ConvertTo-Json -Compress
        exit 0
    }

    $changedFileCount = 0
    $textFiles = Get-ChildItem -LiteralPath $packageRoot -Recurse -File |
        Where-Object { $_.FullName -notlike "$packageRoot\.git\*" }
    foreach ($file in $textFiles) {
        $text = [IO.File]::ReadAllText($file.FullName)
        $updated = $text
        foreach ($pair in $replacements.GetEnumerator()) {
            $updated = $updated.Replace($pair.Key, $pair.Value)
            $updated = $updated.Replace($pair.Key.ToUpperInvariant(), $pair.Value)
        }

        if ($updated -ne $text) {
            [IO.File]::WriteAllText($file.FullName, $updated, $utf8)
            $changedFileCount++
        }
    }

    [ordered]@{
        ok = $true
        packageId = $packageId
        metaCount = $metaFiles.Count
        changedFileCount = $changedFileCount
    } | ConvertTo-Json -Compress
}
finally {
    $sha256.Dispose()
}
