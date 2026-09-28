using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("textcore/font-asset/rebuild",
        Description = "Rebuild one existing dynamic TextCore font from its current imported source font. Refreshes FaceInfo, clears stale glyphs and atlas pixels, synchronizes internal names with the asset filename, and verifies persisted font, source, atlas and material identities. Supports one embedded Alpha8 atlas up to 4096 squared; preserves authoring settings and fallback references.",
        MutatesAssets = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.WritesAssets,
        ErrorCodes = new[] { "invalid_font_asset_path", "unsupported_font_asset", "font_face_load_failed", "font_rebuild_failed", "rollback_failed" },
        Preconditions = new[] { "stable_edit_mode", "single_embedded_dynamic_font_atlas" },
        TransactionScope = "single-textcore-font-asset",
        TransactionAtomicity = "verified-single-asset-rollback",
        TransactionIsolation = "request-serialized",
        TransactionDurability = "editor-session",
        TransactionRollbackKind = "atomic-byte-snapshot",
        TransactionCommitEvidence = new[] { "persisted-face-info", "preserved-asset-identities", "empty-glyph-and-character-tables" },
        CompletionEvidence = "Persisted FaceInfo and unchanged font GUID, source GUID, atlas local ID and material local ID.")]
    public sealed class VmTextCoreFontAssetRebuildTool :
        IVmProjectTool<VmTextCoreFontAssetRebuildRequest, VmTextCoreFontAssetRebuildReport>
    {
        private const long MaximumAssetBytes = 32L * 1024 * 1024;
        private const long MaximumSourceBytes = 64L * 1024 * 1024;

        public VmTextCoreFontAssetRebuildReport Execute(VmTextCoreFontAssetRebuildRequest request)
        {
            string path = request.FontAssetPath?.Replace('\\', '/');
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.Contains("../") || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                throw new VmProjectToolException("invalid_font_asset_path", "fontAssetPath must be an Assets-relative .asset path.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new VmProjectToolException("unsupported_font_asset", "Rebuilding fonts requires stable Edit Mode.");

            FontAsset font = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
            if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Dynamic ||
                font.sourceFontFile == null || font.atlasTextures == null || font.atlasTextures.Length != 1 ||
                font.atlasTextures[0] == null || font.material == null)
                throw new VmProjectToolException("unsupported_font_asset", $"'{path}' must be a dynamic TextCore font with a source, one atlas and a material, using face index zero.");

            Texture2D atlas = font.atlasTextures[0];
            Material material = font.material;
            if (atlas.format != TextureFormat.Alpha8 || font.atlasWidth < 1 || font.atlasWidth > 4096 ||
                font.atlasHeight < 1 || font.atlasHeight > 4096 || AssetDatabase.GetAssetPath(atlas) != path ||
                AssetDatabase.GetAssetPath(material) != path || TableCount(font, "m_GlyphTable") > 65536 || TableCount(font, "m_CharacterTable") > 65536)
                throw new VmProjectToolException("unsupported_font_asset", $"'{path}' needs one embedded Alpha8 atlas up to 4096 squared and at most 65536 glyphs and characters.");

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, path));
            string sourcePath = AssetDatabase.GetAssetPath(font.sourceFontFile);
            if (!sourcePath.StartsWith("Assets/", StringComparison.Ordinal) ||
                new FileInfo(Path.Combine(projectRoot, sourcePath)).Length > MaximumSourceBytes ||
                new FileInfo(absolutePath).Length > MaximumAssetBytes || new FileInfo(absolutePath + ".meta").Length > 65536 ||
                float.IsNaN(font.faceInfo.pointSize) || float.IsInfinity(font.faceInfo.pointSize) ||
                font.faceInfo.pointSize < 1 || font.faceInfo.pointSize > 4096)
                throw new VmProjectToolException("unsupported_font_asset", "The source must be below Assets and at most 64 MiB; the font asset must be at most 32 MiB.");

            string guid = AssetDatabase.AssetPathToGUID(path);
            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            long atlasId = LocalId(atlas);
            long materialId = LocalId(material);
            FontEngineError faceError = FontEngine.LoadFontFace(font.sourceFontFile, (int)font.faceInfo.pointSize);
            if (faceError != FontEngineError.Success)
                throw new VmProjectToolException("font_face_load_failed", $"FontEngine could not load '{sourcePath}': {faceError}.");
            FaceInfo face = FontEngine.GetFaceInfo();
            byte[] originalAsset = File.ReadAllBytes(absolutePath);
            byte[] originalMeta = File.ReadAllBytes(absolutePath + ".meta");
            string expectedName = Path.GetFileNameWithoutExtension(path);

            try
            {
                font.faceInfo = face;
                font.ClearFontAssetData();
                font.name = expectedName;
                atlas.name = font.name + " Atlas";
                material.name = font.name + " Material";
                EditorUtility.SetDirty(atlas);
                EditorUtility.SetDirty(material);
                EditorUtility.SetDirty(font);
                AssetDatabase.SaveAssetIfDirty(font);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                FontAsset saved = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
                if (saved == null || AssetDatabase.AssetPathToGUID(path) != guid ||
                    AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(saved.sourceFontFile)) != sourceGuid ||
                    saved.faceInfo.familyName != face.familyName || saved.faceInfo.styleName != face.styleName ||
                    saved.faceInfo.pointSize != face.pointSize || saved.atlasPopulationMode != AtlasPopulationMode.Dynamic ||
                    saved.atlasTextures.Length != 1 || LocalId(saved.atlasTextures[0]) != atlasId || LocalId(saved.material) != materialId ||
                    TableCount(saved, "m_GlyphTable") != 0 || TableCount(saved, "m_CharacterTable") != 0 ||
                    saved.name != expectedName || saved.atlasTextures[0].name != expectedName + " Atlas" ||
                    saved.material.name != expectedName + " Material")
                    throw new InvalidOperationException("Persisted font face, identities, names or cleared tables differ from the rebuilt product.");

                return new VmTextCoreFontAssetRebuildReport
                {
                    FontAssetPath = path,
                    FontAssetGuid = guid,
                    SourceFontPath = sourcePath,
                    SourceFontGuid = sourceGuid,
                    FamilyName = saved.faceInfo.familyName,
                    StyleName = saved.faceInfo.styleName,
                    PointSize = saved.faceInfo.pointSize,
                    AtlasLocalId = atlasId,
                    MaterialLocalId = materialId,
                    GlyphCount = TableCount(saved, "m_GlyphTable"),
                    CharacterCount = TableCount(saved, "m_CharacterTable"),
                };
            }
            catch (Exception exception)
            {
                try
                {
                    File.WriteAllBytes(absolutePath, originalAsset);
                    File.WriteAllBytes(absolutePath + ".meta", originalMeta);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    if (AssetDatabase.AssetPathToGUID(path) != guid ||
                        !originalAsset.SequenceEqual(File.ReadAllBytes(absolutePath)) ||
                        !originalMeta.SequenceEqual(File.ReadAllBytes(absolutePath + ".meta")))
                        throw new InvalidOperationException("Font rollback byte or GUID verification failed.");
                }
                catch (Exception rollback)
                {
                    throw new VmProjectToolException("rollback_failed", $"Font rebuild failed: {exception.Message}. Rollback failed: {rollback.Message}");
                }
                throw new VmProjectToolException("font_rebuild_failed", $"Font rebuild rolled back: {exception.Message}");
            }
        }

        private static long LocalId(UnityEngine.Object asset)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long localId))
                throw new InvalidOperationException($"'{asset}' has no persistent asset identity.");
            return localId;
        }

        private static int TableCount(FontAsset font, string propertyName)
        {
            using (var serialized = new SerializedObject(font))
                return serialized.FindProperty(propertyName).arraySize;
        }
    }
}
