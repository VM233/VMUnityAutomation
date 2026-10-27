using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmNineSliceCompaction
    {
        internal static VmNineSliceCompactPlan Review(string path, VmImageFingerprintBudget budget,
            out byte[] original, out Color32[] compact)
        {
            string absolute = Absolute(path);
            var plan = new VmNineSliceCompactPlan
            {
                TexturePath = path, SourceSha256 = "", Reason = "", Border = new int[4],
                OriginalBorder = new float[4], TextureGuid = AssetDatabase.AssetPathToGUID(path),
                LocalFileId = "", SpriteId = ""
            };
            original = null;
            compact = null;
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer ||
                importer.textureType != TextureImporterType.Sprite)
            {
                plan.Reason = "notSprite";
                return plan;
            }
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            plan.SourceWidth = plan.CompactWidth = width;
            plan.SourceHeight = plan.CompactHeight = height;
            var sprites = VmAutomationSpriteSheetCommands.LoadSprites(path);
            if (sprites.Count != 1 || sprites[0].rect != new Rect(0, 0, width, height))
            {
                plan.Reason = "requiresOneFullSourceSprite";
                return plan;
            }
            Sprite sprite = sprites[0];
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long localId);
            plan.LocalFileId = localId.ToString(CultureInfo.InvariantCulture);
            if (importer.spriteImportMode == SpriteImportMode.Multiple)
                plan.SpriteId = VmAutomationSpriteSheetCommands.GetSpriteRects(importer)[0].spriteID.ToString();
            Vector4 border = sprite.border;
            plan.OriginalBorder = new[] { border.x, border.y, border.z, border.w };
            if (importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                plan.Reason = "requiresPointUncompressedWithoutMipmaps";
                return plan;
            }
            budget.AdmitFile(new FileInfo(absolute).Length);
            original = VmPngImage.ReadSource(absolute);
            budget.AdmitImage(original, path);
            VmPngImage.ReadDimensions(original, path, out width, out height);
            plan.SourceSha256 = VmPngImage.Hash(original);
            Texture2D texture = VmPngImage.Decode(original, path, width, height);
            try
            {
                Color32[] pixels = texture.GetPixels32();
                var fingerprint = VmNineSliceFingerprint.Create(pixels, width, height, budget);
                plan.Border = fingerprint.CompactionBorder(out int compactWidth, out int compactHeight);
                plan.CompactWidth = compactWidth;
                plan.CompactHeight = compactHeight;
                compact = Compact(pixels, width, height, plan);
                plan.RemovedPixels = width * height - compact.Length;
                plan.ReconstructionVerified = true;
                plan.Eligible = plan.RemovedPixels > 0;
                plan.Reason = plan.Eligible ? "exactReconstruction" : "alreadyMinimal";
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            return plan;
        }

        internal static VmNineSliceCompactPlan Apply(VmNineSliceCompactRequest request)
        {
            VmNineSliceCompactPlan plan = Review(request.TexturePath, new VmImageFingerprintBudget(),
                out byte[] original, out Color32[] compact);
            if (plan.SourceSha256 != request.ExpectedSha256)
                throw new VmProjectToolException("nine_slice_stale_source", "Source PNG changed after review.");
            if (!plan.Eligible)
                throw new VmProjectToolException("nine_slice_ineligible", plan.Reason);
            string absolute = Absolute(request.TexturePath);
            string metaPath = absolute + ".meta";
            if (new FileInfo(metaPath).Length > 1024 * 1024)
                throw new VmProjectToolException("nine_slice_invalid_input", "Importer metadata exceeds 1 MiB.");
            byte[] metadata = File.ReadAllBytes(metaPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(request.TexturePath);
            float ppu = importer.spritePixelsPerUnit;
            Sprite originalSprite = VmAutomationSpriteSheetCommands.LoadSprites(request.TexturePath)[0];
            Vector2 pivot = new Vector2(originalSprite.pivot.x / originalSprite.rect.width,
                originalSprite.pivot.y / originalSprite.rect.height);
            byte[] encoded = VmPngImage.Encode(compact, plan.CompactWidth, plan.CompactHeight, absolute);
            try
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                var border = new Vector4(plan.Border[0], plan.Border[1], plan.Border[2], plan.Border[3]);
                if (importer.spriteImportMode == SpriteImportMode.Multiple)
                {
                    var provider = VmAutomationSpriteSheetCommands.GetSpriteDataProvider(importer);
                    var rects = provider.GetSpriteRects();
                    rects[0].rect = new Rect(0, 0, plan.CompactWidth, plan.CompactHeight);
                    rects[0].border = border;
                    provider.SetSpriteRects(rects);
                    provider.Apply();
                }
                else importer.spriteBorder = border;
                File.WriteAllBytes(absolute, encoded);
                importer.SaveAndReimport();
                Sprite saved = VmAutomationSpriteSheetCommands.LoadSprites(request.TexturePath).Single();
                importer = (TextureImporter)AssetImporter.GetAtPath(request.TexturePath);
                importer.ReadTextureSettings(settings);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(saved, out string guid, out long localId);
                Vector2 savedPivot = new Vector2(saved.pivot.x / saved.rect.width, saved.pivot.y / saved.rect.height);
                string spriteId = importer.spriteImportMode == SpriteImportMode.Multiple
                    ? VmAutomationSpriteSheetCommands.GetSpriteRects(importer)[0].spriteID.ToString() : "";
                if (guid != plan.TextureGuid || localId.ToString(CultureInfo.InvariantCulture) != plan.LocalFileId ||
                    spriteId != plan.SpriteId || saved.rect != new Rect(0, 0, plan.CompactWidth, plan.CompactHeight) ||
                    saved.border != border || (savedPivot - pivot).sqrMagnitude > 0.00000001f ||
                    importer.spritePixelsPerUnit != ppu || importer.filterMode != FilterMode.Point ||
                    settings.spriteMeshType != SpriteMeshType.FullRect || VmPngImage.Hash(File.ReadAllBytes(absolute)) != VmPngImage.Hash(encoded))
                    throw new VmProjectToolException("nine_slice_readback_mismatch", "Native compact Sprite readback differs from the admitted plan.");
                plan.Applied = true;
                return plan;
            }
            catch
            {
                File.WriteAllBytes(absolute, original);
                File.WriteAllBytes(metaPath, metadata);
                AssetDatabase.ImportAsset(request.TexturePath, ImportAssetOptions.ForceUpdate);
                throw;
            }
        }

        private static int Map(int coordinate, int sourceLength, int compactLength, int before, int after)
        {
            if (sourceLength == compactLength) return coordinate;
            return coordinate < before ? coordinate : coordinate >= sourceLength - after
                ? compactLength - sourceLength + coordinate : before;
        }

        private static Color32[] Compact(Color32[] pixels, int width, int height, VmNineSliceCompactPlan plan)
        {
            var output = new Color32[plan.CompactWidth * plan.CompactHeight];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int targetX = Map(x, width, plan.CompactWidth, plan.Border[0], plan.Border[2]);
                    int targetY = Map(y, height, plan.CompactHeight, plan.Border[1], plan.Border[3]);
                    int target = targetY * plan.CompactWidth + targetX;
                    output[target] = pixels[y * width + x];
                }
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (!VmNineSliceFingerprint.VisibleEqual(pixels[y * width + x], output[
                        Map(y, height, plan.CompactHeight, plan.Border[1], plan.Border[3]) * plan.CompactWidth +
                        Map(x, width, plan.CompactWidth, plan.Border[0], plan.Border[2])]))
                        throw new VmProjectToolException("nine_slice_readback_mismatch", "Compaction failed exact source reconstruction.");
            return output;
        }

        private static string Absolute(string path)
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            string absolute = Path.GetFullPath(Path.Combine(root, path));
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !absolute.StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new VmProjectToolException("nine_slice_invalid_input", "An explicit PNG below Assets/ is required.");
            return absolute;
        }
    }
}
