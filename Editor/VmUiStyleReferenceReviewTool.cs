using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("uitoolkit/review-style-references",
        Description = "Compare the actual cached managed asset-reference vector of up to8 explicit USS/UXML inline StyleSheets with their native SerializedObject references. Captures cached wrappers before creating SerializedObject; reports concrete types, Unity liveness and exact object/GUID identities. Does not import, repair, refresh styles or recursively follow templates/imports. Use runtime resource audits separately to establish actual presentation.",
        InputSchemaJson = InputSchema, OutputSchemaJson = OutputSchema,
        ReadOnly = true, SideEffects = VmProjectToolSideEffect.ReadsProjectState,
        ErrorCodes = new[] { "style_reference_path", "style_reference_budget", "style_reference_schema" },
        CompletionEvidence = "Complete bounded comparison of cached managed and native serialized reference identities; no truncation or state repair.")]
    public sealed class VmUiStyleReferenceReviewTool : IVmProjectTool
    {
        internal const int MaximumReferences = 256;
        private const string InputSchema = @"{""type"":""object"",""additionalProperties"":false,""required"":[""assetPaths""],""properties"":{
          ""assetPaths"":{""type"":""array"",""minItems"":1,""maxItems"":8,""uniqueItems"":true,""items"":{""type"":""string"",""minLength"":1,""maxLength"":4096}},
          ""includeMatchingReferences"":{""type"":""boolean""}}}";
        private const string OutputSchema = @"{""type"":""object"",""additionalProperties"":false,
          ""required"":[""success"",""passed"",""scannedSheets"",""scannedReferences"",""mismatchCount"",""sheets""],""properties"":{
          ""success"":{""type"":""boolean""},""passed"":{""type"":""boolean""},""scannedSheets"":{""type"":""integer""},
          ""scannedReferences"":{""type"":""integer""},""mismatchCount"":{""type"":""integer""},
          ""sheets"":{""type"":""array"",""maxItems"":8,""items"":{""$ref"":""#/$defs/sheet""}}},""$defs"":{
          ""reference"":{""type"":""object"",""additionalProperties"":false,""required"":[""clrNull"",""alive"",""runtimeType"",""objectId"",""guid"",""localFileId""],""properties"":{
            ""clrNull"":{""type"":""boolean""},""alive"":{""type"":""boolean""},""runtimeType"":{""type"":[""string"",""null""],""maxLength"":512},
            ""objectId"":{""type"":[""string"",""null""]},""guid"":{""type"":[""string"",""null""]},""localFileId"":{""type"":[""string"",""null""]}}},
          ""pair"":{""type"":""object"",""additionalProperties"":false,""required"":[""index"",""matches"",""managed"",""serialized""],""properties"":{
            ""index"":{""type"":""integer""},""matches"":{""type"":""boolean""},
            ""managed"":{""$ref"":""#/$defs/reference""},""serialized"":{""$ref"":""#/$defs/reference""}}},
          ""sheet"":{""type"":""object"",""additionalProperties"":false,
            ""required"":[""assetPath"",""sheetPresent"",""styleSheetId"",""managedReferenceCount"",""serializedReferenceCount"",""countsMatch"",""mismatchCount"",""references""],""properties"":{
            ""assetPath"":{""type"":""string""},""sheetPresent"":{""type"":""boolean""},""styleSheetId"":{""type"":[""string"",""null""]},
            ""managedReferenceCount"":{""type"":""integer""},""serializedReferenceCount"":{""type"":""integer""},
            ""countsMatch"":{""type"":""boolean""},""mismatchCount"":{""type"":""integer""},
            ""references"":{""type"":""array"",""maxItems"":256,""items"":{""$ref"":""#/$defs/pair""}}}}}}";

        public object Execute(Dictionary<string, object> args)
        {
            string[] paths = VmAutomationUICommandArguments.GetStringList(args, "assetPaths", "assetPaths").ToArray();
            if (paths.Length is < 1 or > 8 || paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length)
                throw new VmProjectToolException("style_reference_budget", "Requires1..8 distinct explicit asset paths.");
            bool includeMatches = args.TryGetValue("includeMatchingReferences", out object value) && Convert.ToBoolean(value);
            var sheets = new List<object>();
            int references = 0, mismatches = 0;
            foreach (string path in paths)
            {
                ValidatePath(path);
                var sheet = LoadSheet(path);
                var result = ReviewSheet(path, sheet, includeMatches);
                references += Math.Max((int)result["managedReferenceCount"], (int)result["serializedReferenceCount"]);
                mismatches += (int)result["mismatchCount"];
                sheets.Add(result);
            }
            return new Dictionary<string, object>
            {
                ["success"] = true, ["passed"] = mismatches == 0,
                ["scannedSheets"] = sheets.Count, ["scannedReferences"] = references,
                ["mismatchCount"] = mismatches, ["sheets"] = sheets
            };
        }

        private static void ValidatePath(string path)
        {
            if (path == null || path.Length > 4096 || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.Contains('\\') || path.Split('/').Any(part => part is ".." or ".") ||
                !(path.EndsWith(".uss", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase)))
                throw new VmProjectToolException("style_reference_path", "Expected an explicit Assets-relative USS/UXML path.");
        }

        private static StyleSheet LoadSheet(string path)
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset is StyleSheet sheet) return sheet;
            if (asset is VisualTreeAsset tree)
                return (StyleSheet)RequireField(typeof(VisualTreeAsset), "inlineSheet", typeof(StyleSheet)).GetValue(tree);
            throw new VmProjectToolException("style_reference_path", $"'{path}' has no imported StyleSheet/VisualTreeAsset.");
        }

        internal static Dictionary<string, object> ReviewSheet(string path, StyleSheet sheet, bool includeMatches)
        {
            var pairs = new List<object>();
            int managedCount = 0, serializedCount = 0, mismatches = 0;
            if (sheet != null)
            {
                var cached = (Object[])RequireField(typeof(StyleSheet), "assets", typeof(Object[])).GetValue(sheet);
                if (cached == null)
                    throw new VmProjectToolException("style_reference_schema", "StyleSheet.assets is null rather than an owned reference vector.");
                ValidateReferenceCount(cached.Length);
                // Preserve the actual cached wrapper state before native serialization can resolve assets.
                var managed = cached.Select(reference => new VmUiStyleReferenceState(reference)).ToArray();
                managedCount = managed.Length;
                using var serialized = new SerializedObject(sheet);
                SerializedProperty property = serialized.FindProperty("assets");
                if (property == null || !property.isArray)
                    throw new VmProjectToolException("style_reference_schema", "StyleSheet has no serialized assets vector.");
                serializedCount = property.arraySize;
                ValidateReferenceCount(serializedCount);
                int count = Math.Max(managedCount, serializedCount);
                for (int index = 0; index < count; index++)
                {
                    var before = index < managedCount ? managed[index] : new VmUiStyleReferenceState(null);
                    var current = new VmUiStyleReferenceState(index < serializedCount
                        ? property.GetArrayElementAtIndex(index).objectReferenceValue : null);
                    bool matches = index < managedCount && index < serializedCount && before.AgreesWith(current);
                    if (!matches) mismatches++;
                    if (!matches || includeMatches)
                        pairs.Add(new Dictionary<string, object>
                        {
                            ["index"] = index, ["matches"] = matches,
                            ["managed"] = before.ToResult(), ["serialized"] = current.ToResult()
                        });
                }
            }
            return new Dictionary<string, object>
            {
                ["assetPath"] = path, ["sheetPresent"] = sheet != null,
                ["styleSheetId"] = sheet != null ? VmObjectId.Get(sheet) : null,
                ["managedReferenceCount"] = managedCount, ["serializedReferenceCount"] = serializedCount,
                ["countsMatch"] = managedCount == serializedCount, ["mismatchCount"] = mismatches,
                ["references"] = pairs
            };
        }

        internal static void ValidateReferenceCount(int count)
        {
            if (count < 0 || count > MaximumReferences)
                throw new VmProjectToolException("style_reference_budget", "A StyleSheet exceeds256 asset references.");
        }

        private static FieldInfo RequireField(Type type, string name, Type fieldType)
        {
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null || field.FieldType != fieldType)
                throw new VmProjectToolException("style_reference_schema", $"'{type.FullName}.{name}' has no supported field shape.");
            return field;
        }
    }
}
