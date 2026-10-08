using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("sprite/update-rects",
        Description = "Update up to 256 named existing Multiple Sprite rectangles and normalized pivots through Unity's Sprite Editor data provider. Preserve texture GUID, Sprite IDs, local file IDs and borders; require Full Rect and verify persisted native readback. Does not rewrite source pixels or create/delete slices.",
        MutatesAssets = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.WritesAssets,
        ErrorCodes = new[] { "sprite_rect_invalid_input", "sprite_rect_invalid_texture", "sprite_rect_readback_mismatch" },
        Preconditions = new[] { "editor-connected", "stable-edit-mode" },
        CompletionEvidence = "The returned native rectangles, normalized pivots, GUID and every Sprite/local-file identity match the admitted request after reimport.")]
    public sealed class VmSpriteRectUpdateTool : IVmProjectTool<VmSpriteRectUpdateRequest, VmSpriteRectUpdateResult>
    {
        internal const int MaximumSprites = 256;

        public VmSpriteRectUpdateResult Execute(VmSpriteRectUpdateRequest request)
        {
            string path = request.TexturePath;
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
                AssetImporter.GetAtPath(path) is not TextureImporter importer ||
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Multiple)
                throw new VmProjectToolException("sprite_rect_invalid_texture", $"'{path}' must be an existing Multiple Sprite texture below Assets/.");

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            var provider = VmAutomationSpriteSheetCommands.GetSpriteDataProvider(importer);
            SpriteRect[] existing = provider.GetSpriteRects();
            if (existing.Length > MaximumSprites || request.Rects.Length > MaximumSprites || request.Rects.Length == 0)
                throw new VmProjectToolException("sprite_rect_invalid_input", $"Update accepts 1 through {MaximumSprites} entries and at most {MaximumSprites} existing Sprites.");

            var byName = existing.ToDictionary(rect => rect.name, StringComparer.Ordinal);
            var updates = new Dictionary<string, VmSpriteRectUpdate>(StringComparer.Ordinal);
            foreach (VmSpriteRectUpdate update in request.Rects)
            {
                if (update.Name.Length > 256 || !byName.ContainsKey(update.Name) || updates.ContainsKey(update.Name) ||
                    !Finite(update.X) || !Finite(update.Y) || !Finite(update.Width) || !Finite(update.Height) ||
                    !Finite(update.PivotX) || !Finite(update.PivotY) || update.X < 0 || update.Y < 0 ||
                    update.Width <= 0 || update.Height <= 0 || (double)update.X + update.Width > width ||
                    (double)update.Y + update.Height > height || update.PivotX < 0 || update.PivotX > 1 ||
                    update.PivotY < 0 || update.PivotY > 1)
                    throw new VmProjectToolException("sprite_rect_invalid_input", $"'{update.Name}' has a duplicate/unknown name, non-finite value or rectangle/pivot outside the source domain {width} x {height}.");
                updates.Add(update.Name, update);
            }

            string guid = AssetDatabase.AssetPathToGUID(path);
            Dictionary<string, long> identities = LocalIds(path);
            foreach (var pair in updates)
            {
                SpriteRect rect = byName[pair.Key];
                VmSpriteRectUpdate update = pair.Value;
                rect.rect = new Rect(update.X, update.Y, update.Width, update.Height);
                rect.pivot = new Vector2(update.PivotX, update.PivotY);
                rect.alignment = SpriteAlignment.Custom;
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            provider.SetSpriteRects(existing);
            provider.Apply();
            importer.SaveAndReimport();

            importer = (TextureImporter)AssetImporter.GetAtPath(path);
            SpriteRect[] saved = VmAutomationSpriteSheetCommands.GetSpriteRects(importer);
            Dictionary<string, long> savedIds = LocalIds(path);
            importer.ReadTextureSettings(settings);
            if (AssetDatabase.AssetPathToGUID(path) != guid || saved.Length != existing.Length ||
                savedIds.Count != identities.Count || settings.spriteMeshType != SpriteMeshType.FullRect)
                throw Mismatch(path, "texture identity, Sprite count or mesh type");

            var rows = new VmSpriteRectIdentity[saved.Length];
            for (int index = 0; index < saved.Length; index++)
            {
                SpriteRect rect = saved[index];
                if (!byName.TryGetValue(rect.name, out SpriteRect expected) || rect.spriteID != expected.spriteID ||
                    !identities.TryGetValue(rect.name, out long expectedId) || !savedIds.TryGetValue(rect.name, out long savedId) ||
                    savedId != expectedId || rect.rect != expected.rect || rect.pivot != expected.pivot || rect.border != expected.border)
                    throw Mismatch(path, $"Sprite '{rect.name}' identity, rectangle, pivot or border");
                rows[index] = new VmSpriteRectIdentity
                {
                    Name = rect.name, SpriteId = rect.spriteID.ToString(),
                    LocalFileId = savedId.ToString(CultureInfo.InvariantCulture),
                    X = rect.rect.x, Y = rect.rect.y, Width = rect.rect.width, Height = rect.rect.height,
                    PivotX = rect.pivot.x, PivotY = rect.pivot.y
                };
            }
            return new VmSpriteRectUpdateResult
            {
                TexturePath = path, TextureGuid = guid, SourceWidth = width, SourceHeight = height,
                PixelsPerUnit = importer.spritePixelsPerUnit, SpriteMeshType = (int)settings.spriteMeshType,
                UpdatedCount = updates.Count, Sprites = rows
            };
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static Dictionary<string, long> LocalIds(string path)
        {
            var result = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (Sprite sprite in VmAutomationSpriteSheetCommands.LoadSprites(path))
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long id))
                    throw Mismatch(path, $"Sprite '{sprite.name}' persistent identity");
                result.Add(sprite.name, id);
            }
            return result;
        }

        private static VmProjectToolException Mismatch(string path, string detail) =>
            new("sprite_rect_readback_mismatch", $"'{path}' did not preserve {detail}.");
    }
}
