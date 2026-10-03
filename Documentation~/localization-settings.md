# Localization settings

Use the official Unity CLI facade and obtain the current `localization/settings`
contract from the bounded catalog. `settingsAssetPath` selects an existing
`UnityEngine.Localization.Settings.LocalizationSettings` asset beneath `Assets`.
The owner registers that asset in EditorBuildSettings and assigns the same
instance to Unity Localization before applying the requested Locale or
initialization updates. Settings and Locale inputs are validated before writes.

For a new project, create the asset through `scriptableobject/create`, create and
register Locales, then call `localization/settings` with `settingsAssetPath` and
the desired `projectLocale` and `selectedLocale`. Do not write the native
EditorBuildSettings file or set a second runtime settings instance.

The response's `settingsAssetPath` identifies the active asset; `changed` includes
`settingsAssetPath` when explicitly selected. An absent active asset, invalid
asset path, wrong asset type, missing asset or unregistered Locale is a failed
owner result. Reopen the project to verify native registration persistence.
