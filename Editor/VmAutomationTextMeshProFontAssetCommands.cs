using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Owns bounded, transactional bitmap-glyph publication into one existing static TMP
    /// font asset. The font, its atlas subasset, and its material identity remain stable.
    /// </summary>
    internal static class VmAutomationTextMeshProFontAssetCommands
    {
        private const int DefaultGlyphPixelHeight = 40;
        private const int DefaultPackingPadding = 1;
        private const int MaxGlyphCount = 16;
        private const int MaxSourceDimension = 512;
        private const int MaxGlyphDimension = 256;
        private const int MaxAtlasDimension = 2048;
        private const long MaxSnapshotBytes = 64L * 1024L * 1024L;
        private const uint FirstPrivateUseUnicode = 0xE000;
        private const uint LastPrivateUseUnicode = 0xF8FF;
        private static readonly object RequestGate = new object();
        private static readonly MethodInfo SetAtlasReadableMethod =
            ResolveSetAtlasReadableMethod();

        internal static object UpsertBitmapGlyphs(Dictionary<string, object> arguments)
        {
            return Execute(arguments, null);
        }

        internal static object UpsertBitmapGlyphsForTesting(
            Dictionary<string, object> arguments, Action afterMutation)
        {
            return Execute(arguments, afterMutation);
        }

        private static object Execute(Dictionary<string, object> arguments,
            Action afterMutation)
        {
            lock (RequestGate)
            {
                AssetSnapshot snapshot = null;
                string fontAssetPath = null;
                string fontAbsolutePath = null;
                string metaAbsolutePath = null;
                Texture2D atlasTexture = null;
                bool atlasWasReadable = false;
                bool readabilityChanged = false;
                bool mutationStarted = false;

                try
                {
                    Request request = ParseRequest(arguments);
                    fontAssetPath = request.FontAssetPath;
                    fontAbsolutePath = ToAbsoluteProjectPath(fontAssetPath);
                    metaAbsolutePath = fontAbsolutePath + ".meta";

                    TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                        fontAssetPath);
                    ValidateFontAsset(fontAsset, fontAssetPath);
                    atlasTexture = fontAsset.atlasTextures[0];

                    if (EditorUtility.IsDirty(fontAsset) || EditorUtility.IsDirty(atlasTexture))
                    {
                        throw new CommandException(
                            $"TMP font asset '{fontAssetPath}' has unsaved Editor changes. Save or discard them before mutating its atlas.",
                            "asset_dirty");
                    }

                    snapshot = CaptureSnapshot(fontAbsolutePath, metaAbsolutePath);
                    int sdfSpread = fontAsset.atlasPadding;
                    if (sdfSpread < 1 || sdfSpread > 16)
                    {
                        throw new CommandException(
                            $"TMP font asset atlas padding must be between 1 and 16, but '{fontAssetPath}' uses {sdfSpread}.",
                            "unsupported_font_asset");
                    }
                    if (request.GlyphPixelHeight <= sdfSpread * 2)
                    {
                        throw new CommandException(
                            $"glyphPixelHeight must be greater than twice the font atlas padding ({sdfSpread}).",
                            "invalid_arguments");
                    }

                    List<PreparedGlyph> prepared = request.Glyphs
                        .Select(glyph => PrepareGlyph(glyph, request.GlyphPixelHeight,
                            sdfSpread))
                        .ToList();

                    atlasWasReadable = atlasTexture.isReadable;
                    mutationStarted = true;
                    if (!atlasWasReadable)
                    {
                        SetAtlasTextureIsReadable(atlasTexture, true);
                        readabilityChanged = true;
                    }

                    byte[] atlasBytes = ReadAlpha8Atlas(atlasTexture);
                    List<PublishedGlyph> published = MutateFontAsset(fontAsset, atlasBytes,
                        prepared, request.PackingPadding);
                    atlasTexture.LoadRawTextureData(atlasBytes);
                    atlasTexture.Apply(false, false);

                    afterMutation?.Invoke();

                    if (!atlasWasReadable)
                    {
                        SetAtlasTextureIsReadable(atlasTexture, false);
                        readabilityChanged = false;
                    }

                    EditorUtility.SetDirty(atlasTexture);
                    EditorUtility.SetDirty(fontAsset);
                    AssetDatabase.SaveAssetIfDirty(fontAsset);
                    AssetDatabase.ImportAsset(fontAssetPath,
                        ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate);

                    string assetSha256 = HashFile(fontAbsolutePath);
                    string metaSha256 = HashFile(metaAbsolutePath);
                    if (!string.Equals(metaSha256, snapshot.MetaSha256,
                            StringComparison.Ordinal))
                    {
                        throw new CommandException(
                            "The TMP font asset meta file changed during glyph publication.",
                            "transaction_postcondition_failed");
                    }

                    VerifyPersistedGlyphs(fontAssetPath, published);

                    var glyphResults = published.Select(glyph => (object)glyph.ToResult())
                        .ToList();
                    return VmAutomationResponse.Success(null, new Dictionary<string, object>
                    {
                        { "fontAssetPath", fontAssetPath },
                        { "atlasWidth", atlasTexture.width },
                        { "atlasHeight", atlasTexture.height },
                        { "atlasFormat", atlasTexture.format.ToString() },
                        { "glyphPixelHeight", request.GlyphPixelHeight },
                        { "sdfSpread", sdfSpread },
                        { "packingPadding", request.PackingPadding },
                        { "glyphs", glyphResults },
                        { "assetSha256", assetSha256 },
                        { "metaSha256", metaSha256 },
                    });
                }
                catch (Exception exception)
                {
                    if (readabilityChanged && atlasTexture != null)
                    {
                        try
                        {
                            SetAtlasTextureIsReadable(
                                atlasTexture, atlasWasReadable);
                        }
                        catch
                        {
                            // The byte snapshot below is the authoritative rollback product.
                        }
                    }

                    string errorCode = exception is CommandException commandException
                        ? commandException.ErrorCode
                        : "tool_execution_failed";
                    if (!mutationStarted || snapshot == null)
                        return VmAutomationResponse.Error(exception.Message, errorCode, false);

                    try
                    {
                        RestoreSnapshot(fontAssetPath, fontAbsolutePath, metaAbsolutePath,
                            snapshot);
                        return VmAutomationResponse.Error(exception.Message, errorCode, false,
                            new Dictionary<string, object>
                            {
                                { "rollbackVerified", true },
                            });
                    }
                    catch (Exception rollbackException)
                    {
                        return VmAutomationResponse.Error(
                            exception.Message + " Rollback failed: " +
                            rollbackException.Message,
                            "rollback_failed", false,
                            new Dictionary<string, object>
                            {
                                { "rollbackVerified", false },
                            });
                    }
                }
            }
        }

        private static Request ParseRequest(Dictionary<string, object> arguments)
        {
            arguments = arguments ?? new Dictionary<string, object>();
            string fontAssetPath = NormalizeAssetPath(GetRequiredString(arguments,
                "fontAssetPath"), ".asset", "fontAssetPath");
            int glyphPixelHeight = GetBoundedInt(arguments, "glyphPixelHeight",
                DefaultGlyphPixelHeight, 8, MaxGlyphDimension);
            int packingPadding = GetBoundedInt(arguments, "packingPadding",
                DefaultPackingPadding, 0, 16);

            if (!arguments.TryGetValue("glyphs", out object glyphsValue) ||
                !(glyphsValue is IList glyphList) || glyphsValue is string)
            {
                throw new CommandException("glyphs must be an array.",
                    "invalid_arguments");
            }
            if (glyphList.Count < 1 || glyphList.Count > MaxGlyphCount)
            {
                throw new CommandException(
                    $"glyphs must contain between 1 and {MaxGlyphCount} entries.",
                    "invalid_arguments");
            }

            var glyphs = new List<GlyphRequest>(glyphList.Count);
            var unicodes = new HashSet<uint>();
            for (int index = 0; index < glyphList.Count; index++)
            {
                Dictionary<string, object> glyph = VmAutomationResponse.ToDictionary(glyphList[index]);
                if (glyph == null)
                {
                    throw new CommandException($"glyphs[{index}] must be an object.",
                        "invalid_arguments");
                }

                int unicodeValue = GetBoundedInt(glyph, "unicode", -1,
                    (int)FirstPrivateUseUnicode, (int)LastPrivateUseUnicode,
                    required: true, fieldPrefix: $"glyphs[{index}].");
                uint unicode = (uint)unicodeValue;
                if (!unicodes.Add(unicode))
                {
                    throw new CommandException(
                        $"glyphs contains duplicate Unicode U+{unicode:X4}.",
                        "invalid_arguments");
                }

                string imagePath = NormalizeAssetPath(GetRequiredString(glyph,
                    "imagePath", $"glyphs[{index}]."), ".png",
                    $"glyphs[{index}].imagePath");
                string absoluteImagePath = ToAbsoluteProjectPath(imagePath);
                if (!File.Exists(absoluteImagePath))
                {
                    throw new CommandException(
                        $"PNG source '{imagePath}' does not exist.",
                        "asset_not_found");
                }
                glyphs.Add(new GlyphRequest(unicode, imagePath, absoluteImagePath));
            }

            return new Request(fontAssetPath, glyphPixelHeight, packingPadding, glyphs);
        }

        private static void ValidateFontAsset(TMP_FontAsset fontAsset, string path)
        {
            if (fontAsset == null)
            {
                throw new CommandException($"TMP font asset '{path}' was not found.",
                    "asset_not_found");
            }
            if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Static)
            {
                throw new CommandException(
                    $"TMP font asset '{path}' must use Static atlas population mode.",
                    "unsupported_font_asset");
            }
            if (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length != 1 ||
                fontAsset.atlasTextures[0] == null)
            {
                throw new CommandException(
                    $"TMP font asset '{path}' must contain exactly one atlas texture.",
                    "unsupported_font_asset");
            }

            Texture2D atlas = fontAsset.atlasTextures[0];
            if (atlas.format != TextureFormat.Alpha8)
            {
                throw new CommandException(
                    $"TMP font asset '{path}' must use an Alpha8 atlas, but uses {atlas.format}.",
                    "unsupported_font_asset");
            }
            if (fontAsset.atlasRenderMode != GlyphRenderMode.SDFAA)
            {
                throw new CommandException(
                    $"TMP font asset '{path}' must use the SDFAA render mode.",
                    "unsupported_font_asset");
            }
            if (atlas.width < 1 || atlas.height < 1 ||
                atlas.width > MaxAtlasDimension || atlas.height > MaxAtlasDimension)
            {
                throw new CommandException(
                    $"TMP font atlas dimensions must be between 1 and {MaxAtlasDimension} pixels per axis.",
                    "unsupported_font_asset");
            }
            if (!string.Equals(AssetDatabase.GetAssetPath(atlas), path,
                    StringComparison.Ordinal))
            {
                throw new CommandException(
                    $"TMP font atlas must be embedded in '{path}'.",
                    "unsupported_font_asset");
            }
            float metricHeight = fontAsset.faceInfo.ascentLine -
                                 fontAsset.faceInfo.descentLine;
            if (!(metricHeight > 0) || float.IsInfinity(metricHeight) ||
                float.IsNaN(metricHeight))
            {
                throw new CommandException(
                    $"TMP font asset '{path}' has an invalid face ascent/descent range.",
                    "unsupported_font_asset");
            }
        }

        private static PreparedGlyph PrepareGlyph(GlyphRequest request,
            int glyphPixelHeight, int sdfSpread)
        {
            byte[] imageBytes = VmAutomationPersistenceFile.ReadAllBytes(request.AbsoluteImagePath);
            if (imageBytes.LongLength > 8L * 1024L * 1024L)
            {
                throw new CommandException(
                    $"PNG source '{request.ImagePath}' exceeds the 8 MB input limit.",
                    "input_too_large");
            }

            Texture2D texture = null;
            try
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                if (!ImageConversion.LoadImage(texture, imageBytes, false))
                {
                    throw new CommandException(
                        $"PNG source '{request.ImagePath}' could not be decoded.",
                        "invalid_image");
                }
                if (texture.width < 1 || texture.height < 1 ||
                    texture.width > MaxSourceDimension ||
                    texture.height > MaxSourceDimension)
                {
                    throw new CommandException(
                        $"PNG source '{request.ImagePath}' must be at most {MaxSourceDimension}x{MaxSourceDimension} pixels.",
                        "invalid_image");
                }

                Color32[] pixels = texture.GetPixels32();
                AlphaBounds bounds = FindAlphaBounds(pixels, texture.width,
                    texture.height, request.ImagePath);
                int contentHeight = glyphPixelHeight - sdfSpread * 2;
                int contentWidth = Math.Max(1, (int)Math.Round(
                    contentHeight * (double)bounds.Width / bounds.Height));
                int glyphPixelWidth = contentWidth + sdfSpread * 2;
                if (glyphPixelWidth > MaxGlyphDimension)
                {
                    throw new CommandException(
                        $"PNG source '{request.ImagePath}' produces a {glyphPixelWidth}x{glyphPixelHeight} glyph, exceeding the {MaxGlyphDimension}-pixel glyph bound.",
                        "invalid_image");
                }

                bool[] inside = ResampleAlphaMask(pixels, texture.width,
                    texture.height, bounds, contentWidth, contentHeight,
                    glyphPixelWidth, glyphPixelHeight, sdfSpread);
                byte[] sdf = CreateSignedDistanceField(inside, glyphPixelWidth,
                    glyphPixelHeight, sdfSpread);
                return new PreparedGlyph(request.Unicode, request.ImagePath,
                    glyphPixelWidth, glyphPixelHeight, sdf);
            }
            finally
            {
                if (texture != null)
                    Object.DestroyImmediate(texture);
            }
        }

        private static AlphaBounds FindAlphaBounds(Color32[] pixels, int width,
            int height, string imagePath)
        {
            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a == 0)
                    continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }

            if (maxX < minX || maxY < minY)
            {
                throw new CommandException(
                    $"PNG source '{imagePath}' contains no visible alpha pixels.",
                    "invalid_image");
            }
            return new AlphaBounds(minX, minY, maxX, maxY);
        }

        private static bool[] ResampleAlphaMask(Color32[] pixels, int sourceWidth,
            int sourceHeight, AlphaBounds bounds, int contentWidth, int contentHeight,
            int targetWidth, int targetHeight, int inset)
        {
            var result = new bool[targetWidth * targetHeight];
            for (int y = 0; y < contentHeight; y++)
            for (int x = 0; x < contentWidth; x++)
            {
                double sourceX = bounds.MinX +
                    ((x + 0.5) * bounds.Width / contentWidth) - 0.5;
                double sourceY = bounds.MinY +
                    ((y + 0.5) * bounds.Height / contentHeight) - 0.5;
                double alpha = SampleAlpha(pixels, sourceWidth, sourceHeight,
                    sourceX, sourceY);
                result[(y + inset) * targetWidth + x + inset] = alpha >= 127.5;
            }
            return result;
        }

        private static double SampleAlpha(Color32[] pixels, int width, int height,
            double x, double y)
        {
            x = Math.Max(0, Math.Min(width - 1, x));
            y = Math.Max(0, Math.Min(height - 1, y));
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = Math.Min(width - 1, x0 + 1);
            int y1 = Math.Min(height - 1, y0 + 1);
            double tx = x - x0;
            double ty = y - y0;
            double bottom = pixels[y0 * width + x0].a * (1 - tx) +
                            pixels[y0 * width + x1].a * tx;
            double top = pixels[y1 * width + x0].a * (1 - tx) +
                         pixels[y1 * width + x1].a * tx;
            return bottom * (1 - ty) + top * ty;
        }

        private static byte[] CreateSignedDistanceField(bool[] inside, int width,
            int height, int spread)
        {
            float[] distanceToInside = DistanceTransform(inside, width, height);
            var outside = new bool[inside.Length];
            for (int index = 0; index < inside.Length; index++)
                outside[index] = !inside[index];
            float[] distanceToOutside = DistanceTransform(outside, width, height);

            var result = new byte[inside.Length];
            for (int index = 0; index < inside.Length; index++)
            {
                double signedDistance = inside[index]
                    ? Math.Sqrt(distanceToOutside[index])
                    : -Math.Sqrt(distanceToInside[index]);
                int value = (int)Math.Round(127.5 +
                    signedDistance * 127.5 / spread);
                result[index] = (byte)Math.Max(0, Math.Min(255, value));
            }
            return result;
        }

        private static float[] DistanceTransform(bool[] features, int width,
            int height)
        {
            const float infinity = 1e20f;
            var intermediate = new float[features.Length];
            var result = new float[features.Length];
            int extent = Math.Max(width, height);
            var source = new float[extent];
            var transformed = new float[extent];
            var vertices = new int[extent];
            var boundaries = new float[extent + 1];

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                    source[x] = features[row + x] ? 0 : infinity;
                DistanceTransform1D(source, width, transformed, vertices,
                    boundaries);
                for (int x = 0; x < width; x++)
                    intermediate[row + x] = transformed[x];
            }

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                    source[y] = intermediate[y * width + x];
                DistanceTransform1D(source, height, transformed, vertices,
                    boundaries);
                for (int y = 0; y < height; y++)
                    result[y * width + x] = transformed[y];
            }
            return result;
        }

        private static void DistanceTransform1D(float[] source, int length,
            float[] result, int[] vertices, float[] boundaries)
        {
            int k = 0;
            vertices[0] = 0;
            boundaries[0] = float.NegativeInfinity;
            boundaries[1] = float.PositiveInfinity;
            for (int q = 1; q < length; q++)
            {
                float intersection;
                do
                {
                    int vertex = vertices[k];
                    intersection = ((source[q] + q * q) -
                                    (source[vertex] + vertex * vertex)) /
                                   (2f * q - 2f * vertex);
                    if (intersection > boundaries[k])
                        break;
                    k--;
                } while (k >= 0);

                if (k < 0)
                {
                    k = 0;
                    vertices[0] = q;
                    boundaries[0] = float.NegativeInfinity;
                    boundaries[1] = float.PositiveInfinity;
                    continue;
                }

                k++;
                vertices[k] = q;
                boundaries[k] = intersection;
                boundaries[k + 1] = float.PositiveInfinity;
            }

            k = 0;
            for (int q = 0; q < length; q++)
            {
                while (boundaries[k + 1] < q)
                    k++;
                int vertex = vertices[k];
                float delta = q - vertex;
                result[q] = delta * delta + source[vertex];
            }
        }

        private static List<PublishedGlyph> MutateFontAsset(TMP_FontAsset fontAsset,
            byte[] atlasBytes, List<PreparedGlyph> prepared, int packingPadding)
        {
            int atlasWidth = fontAsset.atlasTextures[0].width;
            int atlasHeight = fontAsset.atlasTextures[0].height;
            var targetUnicodes = new HashSet<uint>(prepared.Select(item => item.Unicode));
            var existingCharacters = fontAsset.characterTable
                .Where(character => targetUnicodes.Contains(character.unicode))
                .ToDictionary(character => character.unicode, character => character);
            var nonTargetGlyphIndexes = new HashSet<uint>(fontAsset.characterTable
                .Where(character => !targetUnicodes.Contains(character.unicode))
                .Select(character => character.glyphIndex));
            var removableGlyphIndexes = new HashSet<uint>(existingCharacters.Values
                .Select(character => character.glyphIndex)
                .Where(index => !nonTargetGlyphIndexes.Contains(index)));
            var oldGlyphs = fontAsset.glyphTable
                .Where(glyph => removableGlyphIndexes.Contains(glyph.index))
                .ToDictionary(glyph => glyph.index, glyph => glyph);

            foreach (Glyph oldGlyph in oldGlyphs.Values)
                ClearRect(atlasBytes, atlasWidth, atlasHeight, oldGlyph.glyphRect);

            fontAsset.characterTable.RemoveAll(character =>
                targetUnicodes.Contains(character.unicode));
            fontAsset.glyphTable.RemoveAll(glyph =>
                removableGlyphIndexes.Contains(glyph.index));

            var occupancy = new bool[atlasWidth * atlasHeight];
            foreach (Glyph glyph in fontAsset.glyphTable)
                MarkOccupied(occupancy, atlasWidth, atlasHeight, glyph.glyphRect,
                    packingPadding);

            uint nextGlyphIndex = fontAsset.glyphTable.Count == 0
                ? 1
                : fontAsset.glyphTable.Max(glyph => glyph.index) + 1;
            var usedGlyphIndexes = new HashSet<uint>(fontAsset.glyphTable
                .Select(glyph => glyph.index));
            float metricHeight = fontAsset.faceInfo.ascentLine -
                                 fontAsset.faceInfo.descentLine;
            var published = new List<PublishedGlyph>(prepared.Count);

            foreach (PreparedGlyph item in prepared)
            {
                GlyphRect rect;
                bool hasOldCharacter = existingCharacters.TryGetValue(item.Unicode,
                    out TMP_Character oldCharacter);
                uint oldIndex = hasOldCharacter ? oldCharacter.glyphIndex : 0;
                Glyph oldGlyph = null;
                bool hasRemovableOldGlyph = hasOldCharacter &&
                    oldGlyphs.TryGetValue(oldIndex, out oldGlyph);
                bool canReuse = hasRemovableOldGlyph &&
                    oldGlyph.glyphRect.width == item.PixelWidth &&
                    oldGlyph.glyphRect.height == item.PixelHeight &&
                    IsAreaFree(occupancy, atlasWidth, atlasHeight,
                        oldGlyph.glyphRect.x, oldGlyph.glyphRect.y,
                        item.PixelWidth, item.PixelHeight, packingPadding);
                if (canReuse)
                {
                    rect = oldGlyph.glyphRect;
                }
                else if (!TryPlace(occupancy, atlasWidth, atlasHeight,
                             item.PixelWidth, item.PixelHeight, packingPadding,
                             out rect))
                {
                    throw new CommandException(
                        $"The TMP font atlas has no free {item.PixelWidth}x{item.PixelHeight} region for U+{item.Unicode:X4}.",
                        "atlas_full");
                }

                MarkOccupied(occupancy, atlasWidth, atlasHeight, rect,
                    packingPadding);
                WriteRect(atlasBytes, atlasWidth, atlasHeight, rect, item.SdfBytes);

                uint glyphIndex = hasRemovableOldGlyph &&
                                  !usedGlyphIndexes.Contains(oldIndex)
                    ? oldIndex
                    : AllocateGlyphIndex(item.Unicode, usedGlyphIndexes,
                        ref nextGlyphIndex);
                usedGlyphIndexes.Add(glyphIndex);

                float metricWidth = metricHeight * rect.width / rect.height;
                var metrics = new GlyphMetrics(metricWidth, metricHeight, 0,
                    fontAsset.faceInfo.ascentLine, metricWidth);
                var glyph = new Glyph(glyphIndex, metrics, rect, 1, 0);
                var character = new TMP_Character(item.Unicode, fontAsset, glyph);
                fontAsset.glyphTable.Add(glyph);
                fontAsset.characterTable.Add(character);
                published.Add(new PublishedGlyph(item.Unicode, item.ImagePath,
                    glyphIndex, rect, metrics));
            }

            fontAsset.glyphTable.Sort((left, right) =>
                left.index.CompareTo(right.index));
            fontAsset.characterTable.Sort((left, right) =>
                left.unicode.CompareTo(right.unicode));
            UpdatePackingMetadata(fontAsset);
            fontAsset.ReadFontAssetDefinition();
            TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, fontAsset);
            return published;
        }

        private static uint AllocateGlyphIndex(uint unicode,
            HashSet<uint> usedGlyphIndexes, ref uint nextGlyphIndex)
        {
            if (!usedGlyphIndexes.Contains(unicode))
                return unicode;
            while (usedGlyphIndexes.Contains(nextGlyphIndex))
                nextGlyphIndex++;
            return nextGlyphIndex++;
        }

        private static bool TryPlace(bool[] occupancy, int atlasWidth,
            int atlasHeight, int width, int height, int padding,
            out GlyphRect rect)
        {
            int[] prefix = BuildSummedAreaTable(occupancy, atlasWidth, atlasHeight);
            for (int y = 0; y <= atlasHeight - height; y++)
            for (int x = 0; x <= atlasWidth - width; x++)
            {
                if (!IsAreaFree(prefix, atlasWidth, atlasHeight, x, y, width,
                        height, padding))
                    continue;
                rect = new GlyphRect(x, y, width, height);
                return true;
            }
            rect = GlyphRect.zero;
            return false;
        }

        private static bool IsAreaFree(bool[] occupancy, int atlasWidth,
            int atlasHeight, int x, int y, int width, int height, int padding)
        {
            int[] prefix = BuildSummedAreaTable(occupancy, atlasWidth, atlasHeight);
            return IsAreaFree(prefix, atlasWidth, atlasHeight, x, y, width,
                height, padding);
        }

        private static bool IsAreaFree(int[] prefix, int atlasWidth,
            int atlasHeight, int x, int y, int width, int height, int padding)
        {
            int minX = Math.Max(0, x - padding);
            int minY = Math.Max(0, y - padding);
            int maxX = Math.Min(atlasWidth, x + width + padding);
            int maxY = Math.Min(atlasHeight, y + height + padding);
            int stride = atlasWidth + 1;
            int occupied = prefix[maxY * stride + maxX] -
                           prefix[minY * stride + maxX] -
                           prefix[maxY * stride + minX] +
                           prefix[minY * stride + minX];
            return occupied == 0;
        }

        private static int[] BuildSummedAreaTable(bool[] occupancy, int width,
            int height)
        {
            int stride = width + 1;
            var prefix = new int[stride * (height + 1)];
            for (int y = 0; y < height; y++)
            {
                int rowSum = 0;
                for (int x = 0; x < width; x++)
                {
                    if (occupancy[y * width + x])
                        rowSum++;
                    prefix[(y + 1) * stride + x + 1] =
                        prefix[y * stride + x + 1] + rowSum;
                }
            }
            return prefix;
        }

        private static void MarkOccupied(bool[] occupancy, int atlasWidth,
            int atlasHeight, GlyphRect rect, int padding)
        {
            int minX = Math.Max(0, rect.x - padding);
            int minY = Math.Max(0, rect.y - padding);
            int maxX = Math.Min(atlasWidth, rect.x + rect.width + padding);
            int maxY = Math.Min(atlasHeight, rect.y + rect.height + padding);
            for (int y = minY; y < maxY; y++)
            for (int x = minX; x < maxX; x++)
                occupancy[y * atlasWidth + x] = true;
        }

        private static void ClearRect(byte[] atlasBytes, int atlasWidth,
            int atlasHeight, GlyphRect rect)
        {
            int minX = Math.Max(0, rect.x);
            int minY = Math.Max(0, rect.y);
            int maxX = Math.Min(atlasWidth, rect.x + rect.width);
            int maxY = Math.Min(atlasHeight, rect.y + rect.height);
            for (int y = minY; y < maxY; y++)
                Array.Clear(atlasBytes, y * atlasWidth + minX, maxX - minX);
        }

        private static void WriteRect(byte[] atlasBytes, int atlasWidth,
            int atlasHeight, GlyphRect rect, byte[] pixels)
        {
            if (rect.x < 0 || rect.y < 0 || rect.x + rect.width > atlasWidth ||
                rect.y + rect.height > atlasHeight ||
                pixels.Length != rect.width * rect.height)
            {
                throw new CommandException("Prepared glyph rectangle is invalid.",
                    "transaction_preflight_failed");
            }
            for (int y = 0; y < rect.height; y++)
            {
                Buffer.BlockCopy(pixels, y * rect.width, atlasBytes,
                    (rect.y + y) * atlasWidth + rect.x, rect.width);
            }
        }

        private static byte[] ReadAlpha8Atlas(Texture2D atlas)
        {
            var raw = atlas.GetRawTextureData<byte>();
            if (raw.Length != atlas.width * atlas.height)
            {
                throw new CommandException(
                    "TMP Alpha8 atlas byte length does not match its dimensions.",
                    "unsupported_font_asset");
            }
            var bytes = new byte[raw.Length];
            raw.CopyTo(bytes);
            return bytes;
        }

        private static void UpdatePackingMetadata(TMP_FontAsset fontAsset)
        {
            var serialized = new SerializedObject(fontAsset);
            SerializedProperty used = serialized.FindProperty("m_UsedGlyphRects");
            SerializedProperty free = serialized.FindProperty("m_FreeGlyphRects");
            if (used == null || free == null)
            {
                throw new CommandException(
                    "TMP font packing metadata is unavailable in this TextMeshPro version.",
                    "unsupported_font_asset");
            }

            List<GlyphRect> rects = fontAsset.glyphTable
                .Select(glyph => glyph.glyphRect)
                .OrderBy(rect => rect.y)
                .ThenBy(rect => rect.x)
                .ToList();
            used.arraySize = rects.Count;
            for (int index = 0; index < rects.Count; index++)
            {
                SerializedProperty element = used.GetArrayElementAtIndex(index);
                GlyphRect rect = rects[index];
                element.FindPropertyRelative("m_X").intValue = rect.x;
                element.FindPropertyRelative("m_Y").intValue = rect.y;
                element.FindPropertyRelative("m_Width").intValue = rect.width;
                element.FindPropertyRelative("m_Height").intValue = rect.height;
            }
            // This route owns future placement from authoritative glyph rectangles. A static
            // font must not advertise stale free rectangles to a different packer.
            free.arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void VerifyPersistedGlyphs(string fontAssetPath,
            List<PublishedGlyph> expected)
        {
            TMP_FontAsset persisted = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                fontAssetPath);
            if (persisted == null)
            {
                throw new CommandException(
                    "TMP font asset could not be reloaded after publication.",
                    "transaction_postcondition_failed");
            }
            persisted.ReadFontAssetDefinition();
            Texture2D atlas = persisted.atlasTextures[0];
            bool wasReadable = atlas.isReadable;
            bool changedReadability = false;
            try
            {
                if (!wasReadable)
                {
                    SetAtlasTextureIsReadable(atlas, true);
                    changedReadability = true;
                }
                byte[] atlasBytes = ReadAlpha8Atlas(atlas);
                foreach (PublishedGlyph item in expected)
                {
                    TMP_Character character = persisted.characterTable.FirstOrDefault(
                        candidate => candidate.unicode == item.Unicode);
                    Glyph glyph = persisted.glyphTable.FirstOrDefault(candidate =>
                        candidate.index == item.GlyphIndex);
                    if (character == null || glyph == null ||
                        character.glyphIndex != item.GlyphIndex ||
                        !RectsEqual(glyph.glyphRect, item.Rect) ||
                        !ContainsDistanceField(atlasBytes, atlas.width, item.Rect))
                    {
                        throw new CommandException(
                            $"Persisted TMP glyph U+{item.Unicode:X4} did not match the committed glyph and atlas data.",
                            "transaction_postcondition_failed");
                    }
                }
            }
            finally
            {
                if (changedReadability)
                    SetAtlasTextureIsReadable(atlas, false);
                if (changedReadability)
                {
                    AssetDatabase.ImportAsset(fontAssetPath,
                        ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate);

                    // FontEngineEditorUtilities marks the embedded atlas dirty while
                    // toggling readability. Readback restores the original readable
                    // state and does not own a second asset mutation, so do not leak
                    // that Editor-only dirty flag into the next request.
                    EditorUtility.ClearDirty(atlas);
                    TMP_FontAsset reloaded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                        fontAssetPath);
                    if (reloaded?.atlasTextures is { Length: > 0 } &&
                        reloaded.atlasTextures[0] != null)
                    {
                        EditorUtility.ClearDirty(reloaded.atlasTextures[0]);
                    }
                }
            }
        }

        private static bool ContainsDistanceField(byte[] atlasBytes,
            int atlasWidth, GlyphRect rect)
        {
            bool hasInside = false;
            bool hasOutside = false;
            for (int y = rect.y; y < rect.y + rect.height; y++)
            for (int x = rect.x; x < rect.x + rect.width; x++)
            {
                byte value = atlasBytes[y * atlasWidth + x];
                hasInside |= value > 200;
                hasOutside |= value < 50;
            }
            return hasInside && hasOutside;
        }

        private static MethodInfo ResolveSetAtlasReadableMethod()
        {
            const string typeName =
                "UnityEditor.TextCore.LowLevel.FontEngineEditorUtilities";
            Type utilityType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName, false))
                .FirstOrDefault(type => type != null);
            return utilityType?.GetMethod("SetAtlasTextureIsReadable",
                BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic, null,
                new[] { typeof(Texture2D), typeof(bool) }, null);
        }

        private static void SetAtlasTextureIsReadable(Texture2D atlas,
            bool isReadable)
        {
            if (SetAtlasReadableMethod == null)
            {
                throw new CommandException(
                    "Unity's TextCore atlas-readability API is unavailable in this Editor version.",
                    "unsupported_editor_version");
            }
            try
            {
                SetAtlasReadableMethod.Invoke(null, new object[] { atlas, isReadable });
            }
            catch (TargetInvocationException exception)
            {
                throw new CommandException(
                    exception.InnerException?.Message ?? exception.Message,
                    "atlas_readability_failed");
            }
        }

        private static bool RectsEqual(GlyphRect left, GlyphRect right)
        {
            return left.x == right.x && left.y == right.y &&
                   left.width == right.width && left.height == right.height;
        }

        private static AssetSnapshot CaptureSnapshot(string assetPath,
            string metaPath)
        {
            if (!File.Exists(assetPath) || !File.Exists(metaPath))
            {
                throw new CommandException(
                    "TMP font asset and its meta file must both exist on disk.",
                    "asset_not_found");
            }
            byte[] assetBytes = VmAutomationPersistenceFile.ReadAllBytes(assetPath);
            byte[] metaBytes = VmAutomationPersistenceFile.ReadAllBytes(metaPath);
            if (assetBytes.LongLength + metaBytes.LongLength > MaxSnapshotBytes)
            {
                throw new CommandException(
                    "TMP font asset snapshot exceeds the 64 MB transaction limit.",
                    "input_too_large");
            }
            return new AssetSnapshot(assetBytes, metaBytes, Hash(assetBytes),
                Hash(metaBytes));
        }

        private static void RestoreSnapshot(string fontAssetPath, string assetPath,
            string metaPath, AssetSnapshot snapshot)
        {
            VmAutomationPersistenceFile.WriteAllBytes(assetPath, snapshot.AssetBytes);
            VmAutomationPersistenceFile.WriteAllBytes(metaPath, snapshot.MetaBytes);
            // The failed mutation left the loaded font and atlas dirty. Clear only
            // those request-owned flags before importing the authoritative byte
            // snapshot; otherwise Unity preserves the stale in-memory tables.
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(
                         fontAssetPath))
            {
                EditorUtility.ClearDirty(asset);
            }
            AssetDatabase.ImportAsset(fontAssetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            TMP_FontAsset restored = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                fontAssetPath);
            if (restored == null)
            {
                throw new IOException(
                    "Restored TMP font asset could not be reloaded from its transaction snapshot.");
            }
            restored.ReadFontAssetDefinition();
            EditorUtility.ClearDirty(restored);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(
                         fontAssetPath))
            {
                EditorUtility.ClearDirty(asset);
            }
            if (!string.Equals(HashFile(assetPath), snapshot.AssetSha256,
                    StringComparison.Ordinal) ||
                !string.Equals(HashFile(metaPath), snapshot.MetaSha256,
                    StringComparison.Ordinal))
            {
                throw new IOException(
                    "Restored TMP font asset bytes did not match the transaction snapshot.");
            }
        }

        private static string HashFile(string path)
        {
            return Hash(VmAutomationPersistenceFile.ReadAllBytes(path));
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes))
                    .Replace("-", "").ToLowerInvariant();
        }

        private static string GetRequiredString(Dictionary<string, object> values,
            string key, string prefix = "")
        {
            if (!values.TryGetValue(key, out object value) || value == null ||
                string.IsNullOrWhiteSpace(value.ToString()))
            {
                throw new CommandException(prefix + key + " is required.",
                    "invalid_arguments");
            }
            return value.ToString().Trim();
        }

        private static int GetBoundedInt(Dictionary<string, object> values,
            string key, int defaultValue, int minimum, int maximum,
            bool required = false, string fieldPrefix = "")
        {
            if (!values.TryGetValue(key, out object value) || value == null)
            {
                if (!required)
                    return defaultValue;
                throw new CommandException(fieldPrefix + key + " is required.",
                    "invalid_arguments");
            }

            double number;
            try
            {
                number = Convert.ToDouble(value,
                    System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                throw new CommandException(fieldPrefix + key +
                    " must be an integer.", "invalid_arguments");
            }
            if (double.IsNaN(number) || double.IsInfinity(number) ||
                Math.Truncate(number) != number || number < minimum ||
                number > maximum)
            {
                throw new CommandException(
                    $"{fieldPrefix}{key} must be an integer between {minimum} and {maximum}.",
                    "invalid_arguments");
            }
            return (int)number;
        }

        private static string NormalizeAssetPath(string value,
            string requiredExtension, string fieldName)
        {
            string path = (value ?? "").Replace('\\', '/').Trim();
            while (path.StartsWith("./", StringComparison.Ordinal))
                path = path.Substring(2);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(requiredExtension,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new CommandException(
                    $"{fieldName} must be an Assets-relative {requiredExtension} path.",
                    "invalid_arguments");
            }

            string absolute = ToAbsoluteProjectPath(path);
            string assetsRoot = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!absolute.StartsWith(assetsRoot, comparison))
            {
                throw new CommandException(
                    $"{fieldName} must remain below the project Assets folder.",
                    "invalid_arguments");
            }
            return "Assets/" + absolute.Substring(assetsRoot.Length)
                .Replace('\\', '/');
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException("Unity project root is unavailable.");
            return Path.GetFullPath(Path.Combine(projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private sealed class CommandException : Exception
        {
            internal CommandException(string message, string errorCode)
                : base(message)
            {
                ErrorCode = errorCode;
            }

            internal string ErrorCode { get; }
        }

        private sealed class Request
        {
            internal Request(string fontAssetPath, int glyphPixelHeight,
                int packingPadding, List<GlyphRequest> glyphs)
            {
                FontAssetPath = fontAssetPath;
                GlyphPixelHeight = glyphPixelHeight;
                PackingPadding = packingPadding;
                Glyphs = glyphs;
            }

            internal string FontAssetPath { get; }
            internal int GlyphPixelHeight { get; }
            internal int PackingPadding { get; }
            internal List<GlyphRequest> Glyphs { get; }
        }

        private sealed class GlyphRequest
        {
            internal GlyphRequest(uint unicode, string imagePath,
                string absoluteImagePath)
            {
                Unicode = unicode;
                ImagePath = imagePath;
                AbsoluteImagePath = absoluteImagePath;
            }

            internal uint Unicode { get; }
            internal string ImagePath { get; }
            internal string AbsoluteImagePath { get; }
        }

        private sealed class PreparedGlyph
        {
            internal PreparedGlyph(uint unicode, string imagePath, int pixelWidth,
                int pixelHeight, byte[] sdfBytes)
            {
                Unicode = unicode;
                ImagePath = imagePath;
                PixelWidth = pixelWidth;
                PixelHeight = pixelHeight;
                SdfBytes = sdfBytes;
            }

            internal uint Unicode { get; }
            internal string ImagePath { get; }
            internal int PixelWidth { get; }
            internal int PixelHeight { get; }
            internal byte[] SdfBytes { get; }
        }

        private sealed class PublishedGlyph
        {
            internal PublishedGlyph(uint unicode, string imagePath, uint glyphIndex,
                GlyphRect rect, GlyphMetrics metrics)
            {
                Unicode = unicode;
                ImagePath = imagePath;
                GlyphIndex = glyphIndex;
                Rect = rect;
                Metrics = metrics;
            }

            internal uint Unicode { get; }
            internal string ImagePath { get; }
            internal uint GlyphIndex { get; }
            internal GlyphRect Rect { get; }
            internal GlyphMetrics Metrics { get; }

            internal Dictionary<string, object> ToResult()
            {
                return new Dictionary<string, object>
                {
                    { "unicode", (long)Unicode },
                    { "imagePath", ImagePath },
                    { "glyphIndex", (long)GlyphIndex },
                    { "rect", new Dictionary<string, object>
                        {
                            { "x", Rect.x },
                            { "y", Rect.y },
                            { "width", Rect.width },
                            { "height", Rect.height },
                        }
                    },
                    { "metrics", new Dictionary<string, object>
                        {
                            { "width", Metrics.width },
                            { "height", Metrics.height },
                            { "bearingX", Metrics.horizontalBearingX },
                            { "bearingY", Metrics.horizontalBearingY },
                            { "advance", Metrics.horizontalAdvance },
                        }
                    },
                };
            }
        }

        private sealed class AssetSnapshot
        {
            internal AssetSnapshot(byte[] assetBytes, byte[] metaBytes,
                string assetSha256, string metaSha256)
            {
                AssetBytes = assetBytes;
                MetaBytes = metaBytes;
                AssetSha256 = assetSha256;
                MetaSha256 = metaSha256;
            }

            internal byte[] AssetBytes { get; }
            internal byte[] MetaBytes { get; }
            internal string AssetSha256 { get; }
            internal string MetaSha256 { get; }
        }

        private readonly struct AlphaBounds
        {
            internal AlphaBounds(int minX, int minY, int maxX, int maxY)
            {
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
            }

            internal int MinX { get; }
            internal int MinY { get; }
            internal int MaxX { get; }
            internal int MaxY { get; }
            internal int Width => MaxX - MinX + 1;
            internal int Height => MaxY - MinY + 1;
        }
    }
}
