using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VMUnityAutomation.Runtime;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmUiExperienceAuditTests
    {
        private sealed class AuditWindow : EditorWindow { }
        private AuditWindow window;
        private EditorWindow previousFocus;
        private VisualElement surface;
        private Button action;
        private Label requirement;

        [UnitySetUp]
        public IEnumerator AttachNativePanel()
        {
            previousFocus = EditorWindow.focusedWindow;
            window = ScriptableObject.CreateInstance<AuditWindow>();
            window.position = new Rect(100, 100, 480, 400);
            surface = new VisualElement { name = "AuditSurface" };
            Place(surface, 0, 0, 440, 340);
            action = new Button { name = "Action", text = "Continue" };
            Place(action, 20, 20, 180, 48);
            requirement = new Label("Wood 2 / 3") { name = "Requirement" };
            Place(requirement, 20, 90, 280, 40);
            requirement.style.fontSize = 16;
            requirement.style.color = Color.black;
            surface.Add(action);
            surface.Add(requirement);
            window.rootVisualElement.Add(surface);
            window.Show();
            yield return null;
            yield return null;
            Assert.That(surface.panel, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator RetireNativePanel()
        {
            if (window != null) window.Close();
            if (previousFocus != null) previousFocus.Focus();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AlignmentIntentRejectsTopAlignedStretchedSeparatorAndAcceptsCenteredText()
        {
            var chord = new VisualElement();
            Place(chord, 20, 160, 280, 40);
            chord.style.flexDirection = FlexDirection.Row;
            chord.style.alignItems = Align.Center;
            chord.AddToClassList("ui-audit-center-y");
            chord.AddToClassList("ui-audit-row");
            var key = new Button { text = "Control" };
            key.style.width = 90; key.style.height = 30;
            var separator = new Label("+");
            separator.style.height = 30; separator.style.width = 20;
            separator.style.marginTop = separator.style.marginBottom = 0;
            separator.style.paddingTop = separator.style.paddingBottom = 0;
            separator.style.unityTextAlign = TextAnchor.UpperCenter;
            chord.Add(key); chord.Add(separator); surface.Add(chord);
            yield return null;
            AssertRule(Review(), "layout-align-center-y");
            separator.style.unityTextAlign = TextAnchor.MiddleCenter;
            yield return null;
            Assert.That(Review().Findings.Any(f => f.Rule.StartsWith("layout-")), Is.False);
            separator.style.marginTop = 12;
            yield return null;
            AssertRule(Review(), "layout-align-center-y");
            separator.style.display = DisplayStyle.None;
            yield return null;
            Assert.That(Review().Findings.Any(f => f.Rule.StartsWith("layout-")), Is.False);
        }

        [UnityTest]
        public IEnumerator EmptyRowsAndAllAlignmentAxesHaveMatchedNegativeControls()
        {
            var group = new VisualElement();
            Place(group, 20, 160, 180, 100);
            group.AddToClassList("ui-audit-nonempty-rows");
            var child = new VisualElement();
            child.style.width = 40; child.style.height = 30;
            group.Add(child); surface.Add(group);
            yield return null;
            AssertRule(Review(), "layout-empty-row");
            child.Add(new Label("Value"));
            yield return null;
            Assert.That(Review().Findings.Any(f => f.Rule == "layout-empty-row"), Is.False);
            string[] axes = { "left", "right", "top", "bottom", "center-x", "center-y" };
            foreach (string axis in axes)
            {
                group.AddToClassList("ui-audit-align-" + axis);
                if (axis.StartsWith("center")) group.AddToClassList("ui-audit-" + axis);
                child.style.width = 180; child.style.height = 100;
                child.style.marginLeft = child.style.marginTop = 0;
                child.style.flexShrink = 0;
                yield return null;
                Assert.That(Review().Findings.Any(f => f.Rule.StartsWith("layout-align-")), Is.False, axis);
                if (axis == "left" || axis == "right" || axis == "center-x") child.style.marginLeft = 12;
                else child.style.marginTop = 12;
                yield return null;
                AssertRule(Review(), "layout-align-" + axis);
                group.RemoveFromClassList("ui-audit-align-" + axis);
                group.RemoveFromClassList("ui-audit-" + axis);
            }
        }

        [UnityTest]
        public IEnumerator NativeBestFitUsesItsConstraintsAndStillRejectsMinimumFontOverflow()
        {
#if UNITY_6000_3_OR_NEWER
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/VmNativeBestFitAudit.uss");
            System.IO.File.WriteAllText(path, ".native-bestfit-audit { -unity-text-generator: advanced; -unity-text-auto-size: best-fit 10px 28px; }");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            requirement.styleSheets.Add(sheet);
            requirement.AddToClassList("native-bestfit-audit");
            requirement.style.whiteSpace = WhiteSpace.NoWrap;
            requirement.style.width = 70; requirement.style.height = 30;
            requirement.text = "Graphics";
            try
            {
                yield return null;
                yield return null;
                Assert.That(requirement.MeasureTextSize(requirement.text, 0, VisualElement.MeasureMode.Undefined,
                    0, VisualElement.MeasureMode.Undefined).x, Is.GreaterThan(requirement.contentRect.width),
                    "The old unconstrained measurement must reject this valid best-fit witness.");
                Assert.That(Review().Findings.Any(f => f.Rule == "text-overflow"), Is.False);
                requirement.text = new string('W', 100);
                yield return null;
                AssertRule(Review(), "text-overflow");
            }
            finally
            {
                requirement.styleSheets.Remove(sheet);
                requirement.RemoveFromClassList("native-bestfit-audit");
                Assert.That(AssetDatabase.DeleteAsset(path), Is.True);
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator NativeHitTestingDetectsOnlyTheObstructedControl()
        {
            AssertPass(Review());
            var overlay = new VisualElement { name = "BlockingOverlay" };
            Place(overlay, 20, 20, 180, 48);
            surface.Add(overlay);
            yield return null;
            AssertRule(Review(), "hit-target-obstructed");
            overlay.RemoveFromHierarchy();
            yield return null;
            AssertPass(Review());
        }

        [UnityTest]
        public IEnumerator NativeSizeAndAncestorClipHaveMatchedControls()
        {
            AssertPass(Review());
            action.style.width = 12;
            yield return null;
            AssertRule(Review(), "hit-target-small");
            action.style.width = 180;
            var clip = new VisualElement();
            Place(clip, 0, 0, 100, 160);
            clip.style.overflow = Overflow.Hidden;
            surface.Add(clip);
            clip.Add(requirement);
            yield return null;
            AssertRule(Review(), "ancestor-clipping");
            surface.Add(requirement);
            clip.RemoveFromHierarchy();
            yield return null;
            AssertPass(Review());
        }

        [UnityTest]
        public IEnumerator HiddenDisplayAndOverriddenVisibilityFollowNativeSemantics()
        {
            surface.style.display = DisplayStyle.None;
            yield return null;
            Assert.That(VmUiExperienceAudit.Displayed(requirement), Is.False);
            surface.style.display = DisplayStyle.Flex;
            surface.style.visibility = Visibility.Hidden;
            requirement.style.visibility = Visibility.Visible;
            yield return null;
            Assert.That(requirement.resolvedStyle.visibility, Is.EqualTo(Visibility.Visible));
            Assert.That(VmUiExperienceAudit.Displayed(requirement), Is.True);
            requirement.style.visibility = StyleKeyword.Null;
            yield return null;
            Assert.That(VmUiExperienceAudit.Displayed(requirement), Is.False);
        }

        [UnityTest]
        public IEnumerator RequiredInformationCannotBeDimmedByDisabledContainer()
        {
            var contracts = new[] { new VmUiAuditExpectation { Owner = "Eligibility", Element = requirement,
                Displayed = true, MinimumOpacity = .8f } };
            AssertPass(Review(contracts));
            surface.style.opacity = .2f;
            yield return null;
            AssertRule(Review(contracts), "expectation-opacity");
            surface.style.opacity = 1;
            yield return null;
            AssertPass(Review(contracts));
        }

        [UnityTest]
        public IEnumerator ChangedAuthoritativeModelDetectsStaleTextAndEligibility()
        {
            int wood = 2;
            Func<VmUiAuditExpectation[]> capture = () => new[] {
                new VmUiAuditExpectation { Owner = "Inventory", Element = requirement, Text = "Wood " + wood + " / 3" },
                new VmUiAuditExpectation { Owner = "Eligibility", Element = action, Enabled = wood >= 3 } };
            action.SetEnabled(false);
            yield return null;
            AssertPass(Review(capture()));
            wood = 3;
            AssertRule(Review(capture()), "expectation-text");
            AssertRule(Review(capture()), "expectation-enabled");
            requirement.text = "Wood 3 / 3";
            action.SetEnabled(true);
            yield return null;
            AssertPass(Review(capture()));
        }

        [UnityTest]
        public IEnumerator NativeTextMeasurementFindsClippedTranslation()
        {
            AssertPass(Review());
            requirement.style.whiteSpace = WhiteSpace.NoWrap;
            requirement.style.width = 30;
            yield return null;
            AssertRule(Review(), "text-overflow");
            requirement.style.width = 280;
            yield return null;
            AssertPass(Review());
        }

        [UnityTest]
        public IEnumerator BrandContractUsesResolvedNativeAsset()
        {
            var texture = new Texture2D(4, 4);
            try
            {
                var contracts = new[] { new VmUiAuditExpectation { Owner = "Branding", Element = action, Background = texture } };
                AssertRule(Review(contracts), "expectation-background");
                action.style.backgroundImage = new StyleBackground(texture);
                yield return null;
                AssertPass(Review(contracts));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [UnityTest]
        public IEnumerator WrappedTextUsesTheSameDeclaredWidthAndHeightTolerance()
        {
            requirement.text = "Native wrapping boundary";
            requirement.style.whiteSpace = WhiteSpace.Normal;
            requirement.style.paddingLeft = requirement.style.paddingRight = 0;
            requirement.style.paddingTop = requirement.style.paddingBottom = 0;
            yield return null;
            var intrinsic = requirement.MeasureTextSize(requirement.text, 0, VisualElement.MeasureMode.Undefined,
                0, VisualElement.MeasureMode.Undefined);
            requirement.style.width = intrinsic.x - 1;
            requirement.style.height = intrinsic.y;
            yield return null;
            var content = requirement.contentRect;
            var options = Options();
            var strict = requirement.MeasureTextSize(requirement.text, content.width, VisualElement.MeasureMode.AtMost,
                0, VisualElement.MeasureMode.Undefined);
            TestContext.WriteLine($"Native text tolerance: intrinsic={intrinsic}; content={content}; strict={strict}.");
            Assert.That(intrinsic.x - content.width, Is.GreaterThan(0).And.LessThanOrEqualTo(options.GeometryTolerance));
            Assert.That(strict.y, Is.GreaterThan(content.height + options.GeometryTolerance),
                "The native strict-width control must reproduce the extra-line rejection.");
            AssertPass(Review());
            requirement.style.width = intrinsic.x - 4;
            yield return null;
            AssertRule(Review(), "text-overflow");
        }

        [UnityTest]
        public IEnumerator FrameSeamsRejectMovedAndPressedControlsWithoutLosingAncestorScale()
        {
            var frame = new VisualElement { name = "Frame" };
            var tab = new VisualElement { name = "AttachedTab" };
            Place(frame, 20, 160, 200, 80); Place(tab, 220, 173, 60, 54);
            surface.Add(frame); surface.Add(tab);
            var contracts = new[]
            {
                new VmUiAuditExpectation { Owner = "Frame", Element = tab, Geometry = new VmUiAuditExpectation.GeometryContract
                { Kind = VmUiAuditExpectation.GeometryKind.Anchors, Reference = frame, TolerancePixels = 1,
                    ElementAnchor = new Vector2(0, .5f), ReferenceAnchor = new Vector2(1, .5f) } },
                new VmUiAuditExpectation { Owner = "Frame", Element = tab, Geometry = new VmUiAuditExpectation.GeometryContract
                { Kind = VmUiAuditExpectation.GeometryKind.Untransformed } }
            };
            yield return null;
            AssertPass(Review(contracts));
            Assert.That(Review(contracts).GeometryChecks, Is.EqualTo(2));
            tab.style.left = 223;
            yield return null;
            AssertRule(Review(contracts), "visual-anchor");
            tab.style.left = 220;
#if UNITY_2022_2_OR_NEWER
            tab.style.scale = new Scale(new Vector3(.98f, .98f, 1));
#else
            tab.transform.scale = new Vector3(.98f, .98f, 1);
#endif
            yield return null;
            AssertRule(Review(contracts), "visual-attached-transform");
#if UNITY_2022_2_OR_NEWER
            tab.style.scale = new Scale(Vector3.one);
            surface.style.scale = new Scale(new Vector3(1.5f, 1.5f, 1));
#else
            tab.transform.scale = Vector3.one;
            surface.transform.scale = new Vector3(1.5f, 1.5f, 1);
#endif
            yield return null;
            Assert.That(Review(contracts).Findings.Any(f => f.Rule.StartsWith("visual-")), Is.False);
        }

        [UnityTest]
        public IEnumerator IconCanvasRejectsShadowInsetsAnd42PixelShrinkButRetainsSourceTransparency()
        {
            var texture = new Texture2D(16, 16);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * .5f);
            try
            {
                var frame = new VisualElement(); var icon = new VisualElement();
                Place(frame, 20, 160, 54, 54); Place(icon, 3, 3, 48, 48);
                frame.Add(icon); surface.Add(frame); icon.style.backgroundImage = new StyleBackground(sprite);
#if UNITY_2022_2_OR_NEWER
                icon.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
#else
                icon.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
#endif
                var contracts = new[]
                {
                    new VmUiAuditExpectation { Owner = "Slot", Element = icon, Geometry = new VmUiAuditExpectation.GeometryContract
                    { Kind = VmUiAuditExpectation.GeometryKind.Insets, Reference = frame, Insets = Vector4.one * 3, TolerancePixels = 1 } },
                    new VmUiAuditExpectation { Owner = "Slot", Element = icon, Geometry = new VmUiAuditExpectation.GeometryContract
                    { Kind = VmUiAuditExpectation.GeometryKind.PixelScale, PixelUnits = 3, TolerancePixels = 1 } }
                };
                yield return null;
                AssertPass(Review(contracts));
                Place(icon, 6, 6, 42, 42);
                yield return null;
                AssertRule(Review(contracts), "visual-frame-inset");
                AssertRule(Review(contracts), "visual-image-pixels");
                Place(icon, 3, 6, 48, 45);
                yield return null;
                AssertRule(Review(contracts), "visual-frame-inset");
                AssertRule(Review(contracts), "visual-image-pixels");
                icon.style.display = DisplayStyle.None;
                yield return null;
                Assert.Throws<ArgumentException>(() => Review(contracts));
            }
            finally { UnityEngine.Object.DestroyImmediate(sprite); UnityEngine.Object.DestroyImmediate(texture); }
        }

        [UnityTest]
        public IEnumerator ExplicitGapUsesNativeEdgesAndRejectsZeroOrDetachedReferences()
        {
            var first = new VisualElement(); var next = new VisualElement();
            Place(first, 20, 160, 80, 33); Place(next, 70, 202, 30, 30);
            surface.Add(first); surface.Add(next);
            var contract = new VmUiAuditExpectation { Owner = "Party", Element = next,
                Geometry = new VmUiAuditExpectation.GeometryContract { Kind = VmUiAuditExpectation.GeometryKind.Anchors, TolerancePixels = 1,
                    Reference = first, ReferenceAnchor = new Vector2(0, 1), ReferenceOffset = new Vector2(0, 9), CheckX = false } };
            yield return null;
            AssertPass(Review(new[] { contract }));
            next.style.top = 193;
            yield return null;
            AssertRule(Review(new[] { contract }), "visual-anchor");
            first.RemoveFromHierarchy();
            Assert.Throws<ArgumentException>(() => Review(new[] { contract }));
            Assert.That(Review().GeometryChecks, Is.Zero, "No supplied geometry must never report an evaluated visual contract.");
        }

        [Test]
        public void DetachedAndOverBudgetInputsCannotReturnPassingReports()
        {
            Assert.Throws<ArgumentException>(() => VmUiExperienceAudit.Review(new VisualElement(), Options(), Array.Empty<VmUiAuditExpectation>()));
            Assert.Throws<VmUiAuditLimitException>(() => Review(new VmUiAuditExpectation[VmUiExperienceAudit.MaximumExpectations + 1]));
            var parent = surface;
            for (int index = 0; index <= VmUiExperienceAudit.MaximumDepth; index++)
            {
                var child = new VisualElement();
                parent.Add(child);
                parent = child;
            }
            Assert.Throws<VmUiAuditLimitException>(() => Review());
        }

        [UnityTest]
        public IEnumerator DeclaredGeometryScopeDoesNotQualifyCoveredBackgroundControlsOrEmptyInput()
        {
            var overlay = new VisualElement();
            Place(overlay, 20, 20, 180, 48); surface.Add(overlay);
            yield return null;
            AssertRule(Review(), "hit-target-obstructed");
            var options = Options(); options.GeometryOnly = true;
            var contract = new VmUiAuditExpectation { Owner = "Frame", Element = overlay,
                Geometry = new VmUiAuditExpectation.GeometryContract { Kind = VmUiAuditExpectation.GeometryKind.Untransformed } };
            var report = VmUiExperienceAudit.Review(surface, options, new[] { contract });
            AssertPass(report);
            Assert.That(report.MeasurementScope, Is.EqualTo("DeclaredGeometry"));
            Assert.That(report.GeometryChecks, Is.EqualTo(1));
            Assert.That(report.HitTests, Is.Zero);
            Assert.That(report.SemanticCoverage, Is.EqualTo("None"));
            Assert.Throws<ArgumentException>(() => VmUiExperienceAudit.Review(surface, options, Array.Empty<VmUiAuditExpectation>()));
            contract.Geometry = null;
            Assert.Throws<ArgumentException>(() => VmUiExperienceAudit.Review(surface, options, new[] { contract }));
        }

        private VmUiAuditReport Review(IReadOnlyList<VmUiAuditExpectation> expectations = null) =>
            VmUiExperienceAudit.Review(surface, Options(), expectations ?? Array.Empty<VmUiAuditExpectation>());

        private static VmUiAuditOptions Options() => new VmUiAuditOptions
        { SourceIdentity = "native-fixture", Scenario = "matched-control", PixelsPerPanelUnit = 1 };

        private static void AssertPass(VmUiAuditReport report)
        {
            Assert.That(report.Complete, Is.True);
            Assert.That(report.ManualVisualReviewRequired, Is.True);
            Assert.That(report.Passed, Is.True, string.Join("\n", report.Findings.Select(item => item.Rule + ": " + item.ElementName + " " + item.Message +
                " actual=" + item.Observed + " expected=" + item.Expected + " tolerance=" + item.Required)));
            TestContext.WriteLine("Native review: " + report.ElapsedMilliseconds + "ms, " + report.ScannedElements + " nodes.");
        }

        private static void AssertRule(VmUiAuditReport report, string rule)
        {
            Assert.That(report.Complete, Is.True);
            Assert.That(report.Passed, Is.False);
            Assert.That(report.Findings.Any(item => item.Rule == rule), Is.True,
                "Expected " + rule + "; observed " + string.Join(", ", report.Findings.Select(item => item.Rule)));
        }

        private static void Place(VisualElement element, float x, float y, float width, float height)
        {
            element.style.position = Position.Absolute;
            element.style.left = x;
            element.style.top = y;
            element.style.width = width;
            element.style.height = height;
            element.style.marginLeft = 0;
            element.style.marginRight = 0;
            element.style.marginTop = 0;
            element.style.marginBottom = 0;
        }
    }
}
