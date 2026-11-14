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

Reading settings preserves the current runtime settings instance. Only an
explicit `settingsAssetPath` adopts a different instance. A `selectedLocale`
update changes transient Unity Localization state without marking or saving
assets, leaving unrelated dirty resources and live UI documents untouched.
Changes to initialization or project Locale persist only their settings asset.
Explicit `settingsAssetPath` registration additionally publishes the native
EditorBuildSettings binding through Unity's project save boundary.

The 0.7.5 targeted-asset candidate passed transient-selection checks but failed
the existing registration disk witness: saving the settings asset did not save
its EditorBuildSettings reference. The registration path follows Unity's
[config-object contract](https://docs.unity3d.com/ja/6000.0/ScriptReference/EditorBuildSettings.AddConfigObject.html)
and LocalizationEditorSettings' distinct configuration owner. Acceptance must
retain both the saved binding and the unrelated-dirty-asset negative control;
failure of either stops this method.

The fixture restores and saves its original native registration before removing
temporary settings assets. On Unity 6.6, project saves can also expose the
engine's [zero standby worker assertion](https://issuetracker.unity.com/issues/24107/error-failed-to-set-standby-worker-count-to-0-logged-when-setting-standby-import-worker-count-to-0-in-project-settings).
This is an unhandled test failure, not an allowed log; do not suppress it or
report the affected test as passed. A bounded rerun after native settings have
settled must still satisfy every registration and dirty-asset assertion.
