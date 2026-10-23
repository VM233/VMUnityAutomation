using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("ui/rebuild",
        Description = "Rebuild one existing loaded uGUI subtree after Editor authoring or resolution changes. Invalidate text generators, rebuild layout, prepare current text against the native font atlas, and rebuild graphic geometry before requesting a repaint. Preserves serialized text, fonts, sprites, materials, active states and hierarchy. Admits 4096 nodes, 65536 active text characters, font sizes through 512, and 4194304 character/font-size-squared work units.",
        MutatesEditorView = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.ChangesEditorView,
        ErrorCodes = new[] { "ui_root_not_found", "ui_root_rect_required", "ui_rebuild_limit", "ui_text_generation_failed" },
        Preconditions = new[] { "Existing loaded RectTransform GameObject identity from scene/hierarchy" },
        CompletionEvidence = "Scoped text generators were invalidated, native layout rebuilt, active text prepared before graphic geometry, and native repaint requested. Use a visible existing Game View for pixel acceptance.")]
    public sealed class VmUiRebuildTool : IVmProjectTool<VmUiRebuildRequest, VmUiRebuildResult>
    {
        internal const int MaximumNodes = 4096;
        internal const int MaximumCharacters = 65536;
        internal const int MaximumFontSize = 512;
        internal const long MaximumGlyphWork = 4194304;

        public VmUiRebuildResult Execute(VmUiRebuildRequest request)
        {
            var root = VmObjectId.ToObject(request.RootInstanceId) as GameObject;
            if (root == null || !root.scene.IsValid() || !root.scene.isLoaded)
                throw new VmProjectToolException("ui_root_not_found", "The identity must resolve to a loaded scene GameObject.");
            var rectangle = root.transform as RectTransform;
            if (rectangle == null)
                throw new VmProjectToolException("ui_root_rect_required", "The UI root must have a RectTransform.");

            var nodes = new List<Transform> { rectangle };
            var graphics = new List<Graphic>();
            var texts = new List<Text>();
            var preparedTexts = new List<Text>();
            long characters = 0;
            long glyphWork = 0;
            for (int index = 0; index < nodes.Count; index++)
            {
                Transform node = nodes[index];
                if (node.childCount > MaximumNodes - nodes.Count)
                    throw Limit("The subtree exceeds 4096 nodes.");
                for (int child = 0; child < node.childCount; child++)
                    nodes.Add(node.GetChild(child));
                var graphic = node.GetComponent<Graphic>();
                if (graphic == null) continue;
                graphics.Add(graphic);
                if (!(graphic is Text text)) continue;
                texts.Add(text);
                if (!text.IsActive() || text.font == null || text.text.Length == 0) continue;
                int fontSize = text.resizeTextForBestFit ? text.resizeTextMaxSize : text.fontSize;
                if (fontSize < 1 || fontSize > MaximumFontSize)
                    throw Limit("Active text font sizes must be in 1..512.");
                characters += text.text.Length;
                glyphWork += (long)text.text.Length * fontSize * fontSize;
                if (characters > MaximumCharacters || glyphWork > MaximumGlyphWork)
                    throw Limit("The current text exceeds the character or glyph-work budget.");
                preparedTexts.Add(text);
            }

            foreach (Text text in texts)
            {
                text.cachedTextGenerator.Invalidate();
                text.cachedTextGeneratorForLayout.Invalidate();
            }
            rectangle.ForceUpdateRectTransforms();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectangle);
            // Resolve all current glyph requests before publishing any new graphic
            // geometry: a font-atlas rebuild must not leave earlier UVs in use.
            foreach (Text text in preparedTexts)
            {
                var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
                if (!text.cachedTextGenerator.Populate(text.text, settings))
                    throw new VmProjectToolException("ui_text_generation_failed",
                        "Native text generation failed for " + VmAutomationGameObjectCommands.GetHierarchyPath(text.gameObject));
            }
            int rebuilt = 0;
            foreach (Graphic graphic in graphics)
            {
                graphic.SetAllDirty();
                if (!graphic.IsActive()) continue;
                graphic.Rebuild(CanvasUpdate.PreRender);
                graphic.Rebuild(CanvasUpdate.LatePreRender);
                rebuilt++;
            }
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            return new VmUiRebuildResult
            {
                RootInstanceId = VmObjectId.Get(root),
                RootPath = VmAutomationGameObjectCommands.GetHierarchyPath(root),
                NodeCount = nodes.Count, GraphicCount = graphics.Count,
                TextGeneratorsInvalidated = texts.Count, ActiveTextsPrepared = preparedTexts.Count,
                ActiveGraphicsRebuilt = rebuilt, LayoutRebuilt = true
            };
        }

        private static VmProjectToolException Limit(string message) =>
            new VmProjectToolException("ui_rebuild_limit", message);
    }
}
