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
            yield return null;
            var intrinsic = requirement.MeasureTextSize(requirement.text, 0, VisualElement.MeasureMode.Undefined,
                0, VisualElement.MeasureMode.Undefined);
            requirement.style.width = intrinsic.x + 1;
            requirement.style.paddingLeft = .625f;
            requirement.style.paddingRight = .625f;
            requirement.style.height = intrinsic.y + requirement.resolvedStyle.paddingTop + requirement.resolvedStyle.paddingBottom;
            yield return null;
            yield return null;
            var content = requirement.contentRect;
            var strict = requirement.MeasureTextSize(requirement.text, content.width, VisualElement.MeasureMode.AtMost,
                0, VisualElement.MeasureMode.Undefined);
            Assert.That(intrinsic.x - content.width, Is.GreaterThan(0).And.LessThan(Options().GeometryTolerance));
            Assert.That(strict.y, Is.GreaterThan(content.height + Options().GeometryTolerance),
                "The native strict-width control must reproduce the extra-line rejection.");
            AssertPass(Review());
            requirement.style.width = intrinsic.x - 4;
            yield return null;
            AssertRule(Review(), "text-overflow");
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

        private VmUiAuditReport Review(IReadOnlyList<VmUiAuditExpectation> expectations = null) =>
            VmUiExperienceAudit.Review(surface, Options(), expectations ?? Array.Empty<VmUiAuditExpectation>());

        private static VmUiAuditOptions Options() => new VmUiAuditOptions
        { SourceIdentity = "native-fixture", Scenario = "matched-control", PixelsPerPanelUnit = 1 };

        private static void AssertPass(VmUiAuditReport report)
        {
            Assert.That(report.Complete, Is.True);
            Assert.That(report.ManualVisualReviewRequired, Is.True);
            Assert.That(report.Passed, Is.True, string.Join("\n", report.Findings.Select(item => item.Rule + ": " + item.ElementName + " " + item.Message)));
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
