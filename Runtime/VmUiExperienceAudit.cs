using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Runtime
{
    public static class VmUiExperienceAudit
    {
        public const int MaximumPanelElements = 8192;
        public const int MaximumElements = 4096;
        public const int MaximumDepth = 64;
        public const int MaximumHitTests = 128;
        public const int MaximumExpectations = 1024;
        public const int MaximumFindings = 1024;
        public const double MaximumMilliseconds = 500;

        public static VmUiAuditReport Review(VisualElement root, VmUiAuditOptions options,
            IReadOnlyList<VmUiAuditExpectation> expectations)
        {
            if (root == null || root.panel == null)
                throw new ArgumentException("UI audit requires an attached native panel root.", nameof(root));
            if (options == null || !Finite(options.PixelsPerPanelUnit) || options.PixelsPerPanelUnit <= 0 ||
                !Finite(options.MinimumHitPixels) || options.MinimumHitPixels <= 0 ||
                !Finite(options.MinimumTextOpacity) || options.MinimumTextOpacity < 0 || options.MinimumTextOpacity > 1 ||
                !Finite(options.GeometryTolerance) || options.GeometryTolerance < 0 || expectations == null)
                throw new ArgumentException("UI audit requires valid explicit options and an expectation collection.");
            if (expectations.Count > MaximumExpectations)
                throw new VmUiAuditLimitException("UI expectation count exceeds1024.");
            var watch = Stopwatch.StartNew();
            var report = new VmUiAuditReport
            {
                SourceIdentity = options.SourceIdentity, Scenario = options.Scenario, LocaleContext = options.LocaleContext,
                BuildGuid = Application.buildGUID, UnityVersion = Application.unityVersion, Platform = Application.platform.ToString(),
                Frame = Time.frameCount, PixelsPerPanelUnit = options.PixelsPerPanelUnit,
                RootX = root.worldBound.x, RootY = root.worldBound.y,
                RootWidth = root.worldBound.width, RootHeight = root.worldBound.height,
                SemanticExpectations = expectations.Count, SemanticCoverage = expectations.Count == 0 ? "None" : "ProvidedContracts"
            };
            var paths = Inventory(root.panel.visualTree, MaximumPanelElements, watch);
            report.PanelElements = paths.Count;
            var selected = Inventory(root, MaximumElements, watch);
            report.ScannedElements = selected.Count;
            foreach (var entry in selected)
            {
                CheckBudget(watch);
                var element = entry.Key;
                if (!Displayed(element)) continue;
                report.VisibleElements++;
                Rect bounds = element.worldBound;
                if (!Finite(bounds.x) || !Finite(bounds.y) || !Finite(bounds.width) || !Finite(bounds.height))
                {
                    Add(report, element, entry.Value, "nonfinite-layout", "Native visible layout contains a non-finite coordinate.");
                    continue;
                }
                bool control = IsControl(element);
                bool text = element is TextElement label && !string.IsNullOrEmpty(label.text);
                if (!control && !text) continue;
                bool scrollContent = InScrollContent(element);
                Rect visible = VisibleBounds(element, root.worldBound);
                if (visible.width <= 0 || visible.height <= 0)
                {
                    if (!scrollContent) Add(report, element, entry.Value, "viewport-escape", "Visible content is wholly outside its clipping viewport.");
                    continue;
                }
                if (!scrollContent && !Contains(root.worldBound, bounds, options.GeometryTolerance))
                    Add(report, element, entry.Value, "viewport-escape", "Visible content extends outside the selected root viewport.");
                if (!scrollContent && !Contains(visible, bounds, options.GeometryTolerance))
                    Add(report, element, entry.Value, "ancestor-clipping", "An ancestor viewport clips visible text or a control.");
                if (text && !InEditableField(element))
                    CheckText((TextElement)element, entry.Value, report, options);
                if (control && element.enabledInHierarchy && visible.Contains(bounds.center))
                    CheckControl(element, entry.Value, report, options);
            }
            foreach (var expectation in expectations)
            {
                CheckBudget(watch);
                CheckExpectation(selected, expectation, report);
            }
            CheckBudget(watch);
            if (report.MeasuredTexts == 0 && report.HitTests == 0 && report.SemanticExpectations == 0)
                Add(report, root, "root", "empty-presentation", "The selected tree has no measured text, actionable control or supplied state contract.");
            report.ElapsedMilliseconds = watch.Elapsed.TotalMilliseconds;
            report.Complete = true;
            report.Passed = report.Findings.Count == 0;
            return report;
        }

        private static Dictionary<VisualElement, string> Inventory(VisualElement root, int maximum, Stopwatch watch)
        {
            var result = new Dictionary<VisualElement, string>();
            var stack = new Stack<(VisualElement element, string path, int depth)>();
            stack.Push((root, "root", 0));
            while (stack.Count != 0)
            {
                CheckBudget(watch);
                var next = stack.Pop();
                if (next.depth > MaximumDepth || result.Count >= maximum)
                    throw new VmUiAuditLimitException("Native UI tree exceeds its admitted element/depth budget.");
                result.Add(next.element, next.path);
                if (result.Count + stack.Count + next.element.hierarchy.childCount > maximum ||
                    (next.depth == MaximumDepth && next.element.hierarchy.childCount != 0))
                    throw new VmUiAuditLimitException("Native UI tree exceeds its admitted element/depth budget.");
                for (int index = next.element.hierarchy.childCount - 1; index >= 0; index--)
                    stack.Push((next.element.hierarchy[index], next.path + "/" + index, next.depth + 1));
            }
            return result;
        }

        public static bool Displayed(VisualElement element)
        {
            if (element.resolvedStyle.visibility == Visibility.Hidden) return false;
            int depth = 0;
            for (var node = element; node != null; node = node.parent)
            {
                if (++depth > MaximumDepth) throw new VmUiAuditLimitException("UI visibility ancestry exceeds64.");
                if (node.resolvedStyle.display == DisplayStyle.None)
                    return false;
            }
            return true;
        }

        public static float EffectiveOpacity(VisualElement element)
        {
            float opacity = 1;
            int depth = 0;
            for (var node = element; node != null; node = node.parent)
            {
                if (++depth > MaximumDepth) throw new VmUiAuditLimitException("UI opacity ancestry exceeds64.");
                opacity *= node.resolvedStyle.opacity;
            }
            return opacity;
        }

        private static Rect VisibleBounds(VisualElement element, Rect viewport)
        {
            var result = Intersection(element.worldBound, viewport);
            return element.parent == null ? result : Intersection(result, VmUiNativeClip.Read(element.parent));
        }

        private static bool InScrollContent(VisualElement element)
        {
            int depth = 0;
            for (var node = element.parent; node != null; node = node.parent)
            {
                if (++depth > MaximumDepth) throw new VmUiAuditLimitException("UI scroll ancestry exceeds64.");
                if (node is ScrollView scroll && scroll.contentContainer.Contains(element)) return true;
            }
            return false;
        }

        private static bool InEditableField(VisualElement element)
        {
            int depth = 0;
            for (var node = element.parent; node != null; node = node.parent)
            {
                if (++depth > MaximumDepth) throw new VmUiAuditLimitException("UI input ancestry exceeds64.");
                if (node is TextField) return true;
            }
            return false;
        }

        private static void CheckText(TextElement element, string path, VmUiAuditReport report, VmUiAuditOptions options)
        {
            report.MeasuredTexts++;
            float opacity = EffectiveOpacity(element) * element.resolvedStyle.color.a;
            if (opacity < options.MinimumTextOpacity)
                Add(report, element, path, "unreadable-opacity", "Visible text is below the configured effective opacity.", opacity, options.MinimumTextOpacity);
            Rect content = element.contentRect;
            bool wrap = element.resolvedStyle.whiteSpace == WhiteSpace.Normal;
            var measured = element.MeasureTextSize(element.text, wrap ? content.width + options.GeometryTolerance : 0,
                wrap ? VisualElement.MeasureMode.AtMost : VisualElement.MeasureMode.Undefined,
                0, VisualElement.MeasureMode.Undefined);
            if (!Finite(measured.x) || !Finite(measured.y))
                Add(report, element, path, "text-measurement", "Native text measurement is not finite.");
            else if ((!wrap && measured.x > content.width + options.GeometryTolerance) ||
                     measured.y > content.height + options.GeometryTolerance)
                Add(report, element, path, "text-overflow", $"Native text measures {measured.x:R}x{measured.y:R} in a {content.width:R}x{content.height:R} content rectangle with {options.GeometryTolerance:R} geometry tolerance.",
                    wrap ? measured.y : measured.x, wrap ? content.height : content.width);
        }

        private static void CheckControl(VisualElement element, string path, VmUiAuditReport report, VmUiAuditOptions options)
        {
            Rect bounds = element.worldBound;
            float pixels = Mathf.Min(bounds.width, bounds.height) * options.PixelsPerPanelUnit;
            if (pixels < options.MinimumHitPixels)
                Add(report, element, path, "hit-target-small", "Enabled control is below the configured physical hit size.", pixels, options.MinimumHitPixels);
            if (report.HitTests >= MaximumHitTests) throw new VmUiAuditLimitException("UI center hit-test count exceeds128.");
            report.HitTests++;
            var picked = element.panel.Pick(bounds.center);
            var owner = picked;
            while (owner != null && !IsControl(owner)) owner = owner.parent;
            if (owner != element)
                Add(report, element, path, "hit-target-obstructed", "Native center hit testing belongs to another control or overlay.",
                    observed: picked == null ? "No native hit" : picked.name + " (" + picked.GetType().Name + ")");
        }

        private static bool IsControl(VisualElement element) => element is Button || element is BaseField<string> ||
            element is BaseField<float> || element is BaseField<int> || element is BaseField<bool> || element is BaseField<Enum>;

        private static void CheckExpectation(Dictionary<VisualElement, string> paths,
            VmUiAuditExpectation expected, VmUiAuditReport report)
        {
            if (expected == null || expected.Element == null || string.IsNullOrWhiteSpace(expected.Owner) ||
                !paths.TryGetValue(expected.Element, out string path))
                throw new ArgumentException("Every UI expectation requires its actual owner and an element in the selected tree.");
            if (expected.MinimumOpacity.HasValue && (!Finite(expected.MinimumOpacity.Value) ||
                expected.MinimumOpacity.Value < 0 || expected.MinimumOpacity.Value > 1))
                throw new ArgumentException("A UI expectation opacity must be finite and in0..1.");
            var element = expected.Element;
            bool shown = Displayed(element);
            if (expected.Displayed.HasValue && expected.Displayed.Value != shown)
                Add(report, element, path, "expectation-visible", "Displayed state disagrees with its current game-state owner.", owner: expected.Owner,
                    observed: shown.ToString(), expected: expected.Displayed.Value.ToString());
            if (expected.Enabled.HasValue && expected.Enabled.Value != element.enabledInHierarchy)
                Add(report, element, path, "expectation-enabled", "Enabled state disagrees with its current game-state owner.", owner: expected.Owner,
                    observed: element.enabledInHierarchy.ToString(), expected: expected.Enabled.Value.ToString());
            if (expected.Text != null && (!(element is TextElement text) || text.text != expected.Text))
                Add(report, element, path, "expectation-text", "Displayed text disagrees with its current game-state owner.", owner: expected.Owner,
                    observed: element is TextElement actual ? actual.text : element.GetType().Name, expected: expected.Text);
            if (expected.ClassName != null && element.ClassListContains(expected.ClassName) != expected.ClassPresent)
                Add(report, element, path, "expectation-class", "Presentation state class disagrees with its current game-state owner.", owner: expected.Owner,
                    observed: element.ClassListContains(expected.ClassName).ToString(), expected: expected.ClassPresent.ToString());
            if (shown && expected.MinimumOpacity.HasValue && EffectiveOpacity(element) < expected.MinimumOpacity.Value)
                Add(report, element, path, "expectation-opacity", "Required information was dimmed by its actual ancestor chain.",
                    EffectiveOpacity(element), expected.MinimumOpacity.Value, expected.Owner);
            if (shown && expected.Background != null)
            {
                var background = element.resolvedStyle.backgroundImage;
                UnityEngine.Object actual = background.sprite != null ? (UnityEngine.Object)background.sprite : background.texture;
                if (actual != expected.Background)
                    Add(report, element, path, "expectation-background", "Resolved background asset disagrees with its presentation owner.",
                        owner: expected.Owner, observed: actual == null ? "None" : actual.name, expected: expected.Background.name);
            }
        }

        private static void Add(VmUiAuditReport report, VisualElement element, string path, string rule, string message,
            double measured = 0, double required = 0, string owner = "NativeUI", string observed = "", string expected = "")
        {
            if (report.Findings.Count >= MaximumFindings) throw new VmUiAuditLimitException("UI finding count exceeds1024; review is incomplete.");
            report.Findings.Add(new VmUiAuditFinding { Rule = rule, Path = path, ElementName = element.name,
                Owner = owner, Message = message, Measured = measured, Required = required, Observed = observed, Expected = expected });
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Contains(Rect outer, Rect inner, float tolerance) => inner.xMin >= outer.xMin - tolerance &&
            inner.yMin >= outer.yMin - tolerance && inner.xMax <= outer.xMax + tolerance && inner.yMax <= outer.yMax + tolerance;
        private static Rect Intersection(Rect first, Rect second) => Rect.MinMaxRect(Mathf.Max(first.xMin, second.xMin),
            Mathf.Max(first.yMin, second.yMin), Mathf.Min(first.xMax, second.xMax), Mathf.Min(first.yMax, second.yMax));
        private static void CheckBudget(Stopwatch watch)
        {
            if (watch.Elapsed.TotalMilliseconds > MaximumMilliseconds)
                throw new VmUiAuditLimitException("Native UI review exceeded its500ms coherent inspection budget.");
        }
    }
}
