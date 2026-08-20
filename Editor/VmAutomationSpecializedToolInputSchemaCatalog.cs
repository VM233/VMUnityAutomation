using System.Collections.Generic;
using static VMUnityAutomation.Editor.VmAutomationToolInputSchemaComponents;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationSpecializedToolInputSchemaCatalog
    {
        internal static Dictionary<string, object> GetOrNull(string route)
        {
            switch (route)
            {
                case "textcore/sprite-asset/upsert-images":
                {
                    Dictionary<string, object> sprite = VmAutomationToolSchemaFactory.ObjectSchema(
                        VmAutomationToolSchemaFactory.Props(
                            VmAutomationToolSchemaFactory.Prop("name", "string",
                                "Exact Sprite and SpriteCharacter name used by <sprite name=\"...\">."),
                            VmAutomationToolSchemaFactory.Prop("imagePath", "string",
                                "Assets-relative PNG source path. The source asset is retained and need not be importer-readable."),
                            VmAutomationToolSchemaFactory.Prop("glyphScale", "number",
                                "SpriteGlyph scale. Defaults to 1."),
                            VmAutomationToolSchemaFactory.Prop("bearingX", "number",
                                "Horizontal glyph bearing X in atlas pixels. Defaults to 0."),
                            VmAutomationToolSchemaFactory.Prop("bearingY", "number",
                                "Horizontal glyph bearing Y in atlas pixels. Defaults to spriteHeight."),
                            VmAutomationToolSchemaFactory.Prop("advance", "number",
                                "Horizontal glyph advance in atlas pixels. Defaults to spriteWidth."),
                            VmAutomationToolSchemaFactory.Prop("characterScale", "number",
                                "SpriteCharacter scale. Defaults to 1.")),
                        "name", "imagePath");
                    Dictionary<string, object> sprites =
                        (Dictionary<string, object>)VmAutomationToolSchemaFactory.ArrayProp(
                            "sprites", sprite,
                            "One to sixteen uniquely named PNG images to upsert atomically.").Value;
                    sprites["minItems"] = 1;
                    sprites["maxItems"] = 16;

                    Dictionary<string, object> spriteWidth =
                        (Dictionary<string, object>)VmAutomationToolSchemaFactory.Prop(
                            "spriteWidth", "integer",
                            "Raster width assigned to every requested Sprite.").Value;
                    spriteWidth["minimum"] = 1;
                    spriteWidth["maximum"] = 512;
                    Dictionary<string, object> spriteHeight =
                        (Dictionary<string, object>)VmAutomationToolSchemaFactory.Prop(
                            "spriteHeight", "integer",
                            "Raster height assigned to every requested Sprite.").Value;
                    spriteHeight["minimum"] = 1;
                    spriteHeight["maximum"] = 512;
                    Dictionary<string, object> packingPadding =
                        (Dictionary<string, object>)VmAutomationToolSchemaFactory.Prop(
                            "packingPadding", "integer",
                            "Transparent pixels around newly appended Sprite cells. Defaults to 0.").Value;
                    packingPadding["minimum"] = 0;
                    packingPadding["maximum"] = 64;

                    return VmAutomationToolSchemaFactory.StrictSchema(
                        new Dictionary<string, object>
                        {
                            ["spriteAssetPath"] = VmAutomationToolSchemaFactory.Prop(
                                "spriteAssetPath", "string",
                                "Assets-relative path to one existing TextCore SpriteAsset backed by an external Multiple-Sprite PNG atlas.").Value,
                            ["sprites"] = sprites,
                            ["spriteWidth"] = spriteWidth,
                            ["spriteHeight"] = spriteHeight,
                            ["packingPadding"] = packingPadding,
                        },
                        "spriteAssetPath", "sprites", "spriteWidth", "spriteHeight");
                }
                case "textmeshpro/font-asset/upsert-bitmap-glyphs":
                {
                    Dictionary<string, object> glyph = VmAutomationToolSchemaFactory.ObjectSchema(
                        VmAutomationToolSchemaFactory.Props(
                            VmAutomationToolSchemaFactory.Prop("unicode", "integer",
                                "BMP private-use Unicode code point from U+E000 through U+F8FF."),
                            VmAutomationToolSchemaFactory.Prop("imagePath", "string",
                                "Assets-relative PNG source path. The PNG is decoded from file bytes and need not be importer-readable.")),
                        "unicode", "imagePath");
                    Dictionary<string, object> glyphs =
                        (Dictionary<string, object>)VmAutomationToolSchemaFactory.ArrayProp(
                            "glyphs", glyph,
                            "One to sixteen unique private-use bitmap glyphs to upsert atomically.").Value;
                    glyphs["minItems"] = 1;
                    glyphs["maxItems"] = 16;

                    Dictionary<string, object> glyphPixelHeight =
                        (Dictionary<string, object>)VmAutomationToolSchemaFactory.Prop(
                            "glyphPixelHeight", "integer",
                            "Atlas raster height per glyph. Defaults to 40; the font face ascent/descent owns layout size.").Value;
                    glyphPixelHeight["minimum"] = 8;
                    glyphPixelHeight["maximum"] = 256;
                    Dictionary<string, object> packingPadding =
                        (Dictionary<string, object>)VmAutomationToolSchemaFactory.Prop(
                            "packingPadding", "integer",
                            "Empty atlas pixels reserved around placed glyph rectangles. Defaults to 1.").Value;
                    packingPadding["minimum"] = 0;
                    packingPadding["maximum"] = 16;

                    return VmAutomationToolSchemaFactory.StrictSchema(
                        new Dictionary<string, object>
                        {
                            ["fontAssetPath"] = VmAutomationToolSchemaFactory.Prop(
                                "fontAssetPath", "string",
                                "Assets-relative path to one existing static TMP font asset with an embedded Alpha8 SDFAA atlas.").Value,
                            ["glyphs"] = glyphs,
                            ["glyphPixelHeight"] = glyphPixelHeight,
                            ["packingPadding"] = packingPadding,
                        },
                        "fontAssetPath", "glyphs");
                }
                case "texture/apply-sprite-preset":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Texture asset path."),
                        VmAutomationToolSchemaFactory.Prop("referencePath", "string", "Optional texture asset whose importer settings are copied first."),
                        VmAutomationToolSchemaFactory.Prop("preset", "string", "High-level preset. Supported: pixel-sprite. Preserves the current Single/Multiple mode."),
                        VmAutomationToolSchemaFactory.Prop("pixelsPerUnit", "number", "Sprite pixels per unit."),
                        VmAutomationToolSchemaFactory.Prop("filterMode", "string", "Texture FilterMode, e.g. Point."),
                        VmAutomationToolSchemaFactory.Prop("textureCompression", "string", "TextureImporterCompression value."),
                        VmAutomationToolSchemaFactory.Prop("defaultPlatformFormat", "string", "Default platform TextureImporterFormat, e.g. RGBA32."),
                        VmAutomationToolSchemaFactory.Prop("defaultPlatformCompression", "string", "Default platform TextureImporterCompression."),
                        VmAutomationToolSchemaFactory.Prop("readable", "boolean", "Texture is readable."),
                        VmAutomationToolSchemaFactory.Prop("mipmapEnabled", "boolean", "Generate mipmaps."),
                        VmAutomationToolSchemaFactory.Prop("alphaIsTransparency", "boolean", "Alpha is transparency."),
                        VmAutomationToolSchemaFactory.Vector2Prop("pivot", "Sprite pivot with x/y."),
                        SpriteBorderProp("border", "Sprite border. Accepts number, [left,bottom,right,top], or object with left/bottom/right/top.")
                    ), "path");
                case "texture/info":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Texture asset path under Assets/.")
                    ), "path");
                case "texture/set-import":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Texture asset path under Assets/."),
                        VmAutomationToolSchemaFactory.Prop("textureType", "string", "TextureImporterType, such as Default, Sprite, or NormalMap."),
                        VmAutomationToolSchemaFactory.Prop("spriteMode", "string", "SpriteImportMode, such as Single or Multiple."),
                        VmAutomationToolSchemaFactory.Prop("spritePixelsPerUnit", "number", "Sprite pixels per unit."),
                        VmAutomationToolSchemaFactory.Prop("sRGB", "boolean", "Import as sRGB texture."),
                        VmAutomationToolSchemaFactory.Prop("readable", "boolean", "Enable CPU read/write access."),
                        VmAutomationToolSchemaFactory.Prop("mipmapEnabled", "boolean", "Generate mipmaps."),
                        VmAutomationToolSchemaFactory.Prop("filterMode", "string", "Texture FilterMode."),
                        VmAutomationToolSchemaFactory.Prop("wrapMode", "string", "TextureWrapMode."),
                        VmAutomationToolSchemaFactory.Prop("maxTextureSize", "number", "Maximum imported texture size."),
                        VmAutomationToolSchemaFactory.Prop("textureCompression", "string", "TextureImporterCompression value."),
                        VmAutomationToolSchemaFactory.Prop("anisoLevel", "number", "Anisotropic filtering level."),
                        VmAutomationToolSchemaFactory.Prop("alphaIsTransparency", "boolean", "Treat alpha as transparency."),
                        VmAutomationToolSchemaFactory.Prop("npotScale", "string", "TextureImporterNPOTScale value.")
                    ), "path");
                case "texture/find-duplicates":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("folder", "string", "Single search folder under Assets/. Defaults to Assets."),
                        VmAutomationToolSchemaFactory.ArrayProp("folders", "string", "Additional search folders under Assets/. Results are de-duplicated across folders."),
                        VmAutomationToolSchemaFactory.Prop("mode", "string", "Comparison mode: decodedPixels (default) or fileBytes."),
                        VmAutomationToolSchemaFactory.ArrayProp("extensions", "string", "Optional file extensions such as png, jpg, or jpeg. decodedPixels supports PNG/JPEG."),
                        VmAutomationToolSchemaFactory.Prop("maxAssets", "number", "Maximum assets to fingerprint. Defaults to 10000; capped at 50000."),
                        VmAutomationToolSchemaFactory.Prop("maxGroups", "number", "Maximum duplicate groups returned. Defaults to 100; capped at 2000.")
                    ));
                case "texture/import-image":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("sourcePath", "string", "Local image file path."),
                        VmAutomationToolSchemaFactory.Prop("sourceUrl", "string", "Remote image URL."),
                        VmAutomationToolSchemaFactory.Prop("targetPath", "string", "Target asset path inside Assets."),
                        VmAutomationToolSchemaFactory.Prop("targetFolder", "string", "Target folder used with assetName."),
                        VmAutomationToolSchemaFactory.Prop("assetName", "string", "Target file name used with targetFolder."),
                        VmAutomationToolSchemaFactory.Prop("overwrite", "boolean", "Overwrite targetPath if content differs. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("dedupeByHash", "boolean", "Skip if the target folder already contains identical image bytes. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("applySpritePreset", "boolean", "Apply sprite import settings after import. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("preset", "string", "Preset passed to texture/apply-sprite-preset. Defaults to pixel-sprite.")
                    ));
                case "texture/check-import-settings":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Texture asset path to check."),
                        VmAutomationToolSchemaFactory.ArrayProp("assetPaths", "string", "Texture asset paths to check."),
                        VmAutomationToolSchemaFactory.Prop("folderPath", "string", "Folder to scan recursively for Texture2D assets."),
                        VmAutomationToolSchemaFactory.Prop("referencePath", "string", "Optional texture asset whose importer settings are treated as expected."),
                        VmAutomationToolSchemaFactory.Prop("preset", "string", "Optional high-level preset to check. Supported: pixel-sprite."),
                        VmAutomationToolSchemaFactory.Prop("requirePixelSprite", "boolean", "Shortcut for preset=pixel-sprite. Defaults to true when referencePath is omitted."),
                        VmAutomationToolSchemaFactory.Prop("includeMatching", "boolean", "Include passing comparisons in the returned comparisons list. Defaults to false.")
                    ));
                case "texture/check-ui-import-settings":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Texture asset path to check."),
                        VmAutomationToolSchemaFactory.ArrayProp("assetPaths", "string", "Texture asset paths to check."),
                        VmAutomationToolSchemaFactory.Prop("folderPath", "string", "Folder to scan recursively for Texture2D assets."),
                        VmAutomationToolSchemaFactory.Prop("referencePath", "string", "Optional texture asset whose importer settings are treated as expected."),
                        VmAutomationToolSchemaFactory.Prop("includeMatching", "boolean", "Include passing comparisons in the returned comparisons list. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("expectedWidth", "number", "Optional exact texture width check."),
                        VmAutomationToolSchemaFactory.Prop("expectedHeight", "number", "Optional exact texture height check."),
                        SpriteBorderObjectProp("expectedBorder", "Optional sprite border check. Accepts object with left/bottom/right/top or x/y/z/w."),
                        VmAutomationToolSchemaFactory.Prop("maxTextureSize", "number", "Optional exact TextureImporter maxTextureSize check."),
                        VmAutomationToolSchemaFactory.Prop("tolerance", "number", "Float tolerance for border/PPU checks. Defaults to 0.001.")
                    ));
                case "build/start":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("target", "string", "BuildTarget. Defaults to StandaloneWindows64."),
                        VmAutomationToolSchemaFactory.Prop("outputPath", "string", "Player output executable path."),
                        VmAutomationToolSchemaFactory.Prop("developmentBuild", "boolean", "Build with Development flag."),
                        VmAutomationToolSchemaFactory.ArrayProp("scenes", "string", "Optional scene paths. Defaults to enabled Build Settings scenes."),
                        VmAutomationToolSchemaFactory.Prop("overwrite", "boolean", "Delete existing exe and Data folder before build. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("run", "boolean", "Launch the built executable after a successful build. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("runSeconds", "number", "Seconds to let the executable run before sampling/termination. Defaults to 5."),
                        VmAutomationToolSchemaFactory.Prop("terminateAfter", "boolean", "Kill the process after sampling. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("captureWindow", "boolean", "Capture the built player's main window on Windows. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("screenshotPath", "string", "PNG path for captureWindow output."),
                        VmAutomationToolSchemaFactory.Prop("windowWaitMs", "number", "Milliseconds to wait for the main window. Defaults to 5000."),
                        VmAutomationToolSchemaFactory.Prop("logTailLines", "number", "Player.log tail lines to return. Defaults to 120."),
                        VmAutomationToolSchemaFactory.Prop("clearStuck", "boolean", "Replace a non-terminal build job left behind by an interrupted editor session. Defaults to false.")
                    ), "outputPath");
                case "undo/perform":
                case "undo/redo":
                {
                    Dictionary<string, object> schema = VmAutomationToolSchemaFactory.StrictSchema(
                        VmAutomationToolSchemaFactory.Props(
                            VmAutomationToolSchemaFactory.Prop("actionId", "number",
                                "Exact Automation action-history identity."),
                            VmAutomationToolSchemaFactory.Prop("requestId", "number",
                                "Exact Automation queue request identity.")));
                    schema["anyOf"] = new List<object>
                    {
                        new Dictionary<string, object> { { "required", new List<object> { "actionId" } } },
                        new Dictionary<string, object> { { "required", new List<object> { "requestId" } } },
                    };
                    return schema;
                }
                case "undo/history":
                    return VmAutomationToolSchemaFactory.StrictSchema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("limit", "number",
                            "Maximum recent Automation request records. Defaults to 50; capped at 200.")));
                case "undo/clear":
                    return VmAutomationToolSchemaFactory.StrictSchema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("confirm", "boolean",
                            "Required explicit acknowledgement of irreversible Undo-history deletion."),
                        VmAutomationToolSchemaFactory.Prop("objectPath", "string",
                            "Optional scene GameObject path. Omit to clear global Unity Undo history.")),
                        "confirm");
                case "build/get-job":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("jobId", "string", "Optional build job ID. Defaults to the current or latest job."),
                        VmAutomationToolSchemaFactory.Prop("clear", "boolean", "Clear the persisted job after a terminal result is read. Defaults to false.")
                    ));

                default:
                    return null;
            }
        }
    }
}
