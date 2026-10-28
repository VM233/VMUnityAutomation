using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmUIBuilderPreviewTargetTests
    {
        private sealed class PreviewWindow : EditorWindow { }
        private static VisualElement Spacer(float height) => new VisualElement { style = { height = height, flexShrink = 0 } };
        private static ScrollView Scroller(float height) => new ScrollView { style = { height = height, width = 300, flexShrink = 0 } };
        [UnityTest]
        public IEnumerator NestedNativeScrollsRevealOneActualTarget()
        {
            var focus = EditorWindow.focusedWindow;
            var window = ScriptableObject.CreateInstance<PreviewWindow>();
            window.position = new Rect(100, 100, 420, 400);
            var outer = Scroller(150); var middle = Scroller(240); var inner = Scroller(200);
            var target = new Label("The actual last sample") { name = "LastSample", style = { height = 40, width = 200, flexShrink = 0 } };
            window.rootVisualElement.Add(outer); outer.Add(Spacer(250)); outer.Add(middle);
            middle.Add(Spacer(350)); middle.Add(inner); inner.Add(Spacer(500)); inner.Add(target);
            window.Show();
            try
            {
                yield return null; yield return null;
                Assert.That(VmAutomationUIBuilderPreviewCommands.IsViewportFramed(target.worldBound, inner.contentViewport.worldBound, 1), Is.False);
                var reveal = new VmUIBuilderPreviewTarget(window.rootVisualElement, target.name);
                for (int frame = 0; !reveal.Complete && frame < 64; frame++) { reveal.Advance(); yield return null; }
                Assert.That(reveal.Complete, Is.True);
                bool visible = false;
                for (int frame = 0; !visible && frame < 16; frame++) { visible = reveal.Observe(1); yield return null; }
                Assert.That(visible, Is.True, "The exact target must fit all three actual native clip viewports.");
                Assert.That(reveal.Receipt()["completedScrolls"], Is.EqualTo(3));
                Assert.That(outer.scrollOffset.y, Is.GreaterThan(0)); Assert.That(middle.scrollOffset.y, Is.GreaterThan(0));
                Assert.That(inner.scrollOffset.y, Is.GreaterThan(0));
                Assert.Throws<InvalidOperationException>(() => reveal.Advance());
                target.RemoveFromHierarchy();
                Assert.Throws<InvalidOperationException>(() => reveal.Observe(1));
            }
            finally { window.Close(); if (focus != null) focus.Focus(); }
        }
        [UnityTest]
        public IEnumerator InvalidTargetsNeverSelectAnotherSample()
        {
            var focus = EditorWindow.focusedWindow;
            var window = ScriptableObject.CreateInstance<PreviewWindow>();
            var root = window.rootVisualElement; var scroll = Scroller(100);
            var target = new Label("Too tall") { name = "Target", style = { height = 400, flexShrink = 0 } };
            scroll.Add(target); root.Add(scroll); window.Show();
            try
            {
                yield return null; yield return null;
                Assert.Throws<InvalidOperationException>(() => new VmUIBuilderPreviewTarget(root, "Missing"));
                var duplicate = new Label { name = target.name }; scroll.Add(duplicate);
                Assert.Throws<InvalidOperationException>(() => new VmUIBuilderPreviewTarget(root, target.name));
                duplicate.RemoveFromHierarchy();
                var oversized = new VmUIBuilderPreviewTarget(root, target.name);
                Assert.Throws<InvalidOperationException>(() => oversized.Advance());
                target.style.display = DisplayStyle.None; yield return null;
                Assert.Throws<InvalidOperationException>(() => new VmUIBuilderPreviewTarget(root, target.name));
                var detached = new VisualElement(); detached.Add(new Label { name = "Detached" });
                Assert.Throws<InvalidOperationException>(() => new VmUIBuilderPreviewTarget(detached, "Detached"));
                var excessive = new VisualElement();
                for (int index = 0; index < 2501; index++) excessive.Add(new VisualElement { name = "N" + index });
                Assert.Throws<InvalidOperationException>(() => new VmUIBuilderPreviewTarget(excessive, "N2500"));
            }
            finally { window.Close(); if (focus != null) focus.Focus(); }
        }
    }
}
