using System.Collections.Generic;
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
    public sealed class VmUIToolkitNativeHierarchyTests
    {
        private sealed class PointerWindow : EditorWindow { }

        [UnityTest]
        public IEnumerator PointerDispatchRunsNativeClickableAndSliderCapture()
        {
            var window = ScriptableObject.CreateInstance<PointerWindow>();
            window.titleContent = new GUIContent("Native UI Pointer Test");
            window.position = new Rect(100, 100, 420, 220);
            int clicks = 0;
            var button = new Button(() => clicks++) { text = "Action" };
            button.style.height = 60;
            var slider = new Slider(0, 100);
            slider.style.height = 80;
            window.rootVisualElement.Add(button);
            window.rootVisualElement.Add(slider);
            window.Show();
            try
            {
                yield return null;
                yield return null;
                var root = window.rootVisualElement;
                var point = button.worldBound.center;
                Assert.That(root.panel.Pick(point), Is.SameAs(button));
                VmAutomationUIToolkitCommands.DispatchNativePointer(button, point, "Down");
                VmAutomationUIToolkitCommands.DispatchNativePointer(button, point, "Up");
                Assert.That(clicks, Is.EqualTo(1));
                var track = slider.Q(className: "unity-base-slider__tracker").worldBound;
                var start = new Vector2(track.xMin + track.width * .2f, track.center.y);
                var end = new Vector2(track.xMin + track.width * .8f, track.center.y);
                VmAutomationUIToolkitCommands.DispatchNativePointer(root.panel.Pick(start), start, "Down");
                VmAutomationUIToolkitCommands.DispatchNativePointer(root.panel.Pick(end), end, "Move");
                VmAutomationUIToolkitCommands.DispatchNativePointer(root.panel.Pick(end), end, "Up");
                Assert.That(slider.value, Is.GreaterThan(50));
            }
            finally { window.Close(); }
        }

        [Test]
        public void GeneratedScrollPartsPublishResolvableNativePaths()
        {
            var root = new VisualElement();
            var scroll = new ScrollView();
            root.Add(scroll);
            scroll.Add(new Label("Authored content"));
            var entries = new List<Dictionary<string,object>>();
            VmAutomationUIToolkitElementUtility.CollectGeneratedChildren(root,scroll,"root/0",0,6,
                true,new List<string>(),new List<string>(),entries);
            var lowPath = VmAutomationUIToolkitElementUtility.GetElementPath(root,scroll.verticalScroller.lowButton);
            Assert.That(lowPath,Is.Not.Empty);
            Assert.That(VmAutomationUIToolkitElementUtility.GetElementByPath(root,lowPath),Is.SameAs(scroll.verticalScroller.lowButton));
            Assert.That(entries.Exists(entry => (string)entry["path"] == lowPath),Is.True);
            Assert.That(entries.Exists(entry => (string)entry["type"] == nameof(Scroller)),Is.True);
        }

        [Test]
        public void QueriesAndNamedPathsReachGeneratedSliderParts()
        {
            var root = new VisualElement();
            var scroll = new ScrollView { name = "Scroll" };
            scroll.verticalScroller.lowButton.name = "NativeLow";
            root.Add(scroll);
            var query = new Dictionary<string,object> { { "name","NativeLow" } };
            var found = VmAutomationUIToolkitElementUtility.FindEditorElement(root,query,out var path,out var error);
            Assert.That(error,Is.Empty);
            Assert.That(found,Is.SameAs(scroll.verticalScroller.lowButton));
            Assert.That(VmAutomationUIToolkitElementUtility.GetElementByPath(root,path),Is.SameAs(found));
            Assert.That(VmAutomationUIToolkitElementUtility.GetElementByVisualElementPath(root,
                new List<string> { "Scroll","NativeLow" }),Is.SameAs(found));
        }

        [Test]
        public void TreeCountsAndNodeBoundsDescribeTheSameNativeGraph()
        {
            var scroll = new ScrollView();
            int count = 0;
            bool truncated = false;
            var tree = VmAutomationUIToolkitElementUtility.BuildElementTree(scroll,"root",0,8,4,false,
                ref count,ref truncated);
            Assert.That((int)tree["childCount"],Is.EqualTo(scroll.hierarchy.childCount));
            Assert.That(scroll.hierarchy.childCount,Is.GreaterThan(scroll.childCount));
            Assert.That(count,Is.EqualTo(4));
            Assert.That(truncated,Is.True);
        }

        [Test]
        public void ObservationPreservesAuthoredContentOwnership()
        {
            var scroll = new ScrollView();
            var content = new Label("Content");
            scroll.Add(content);
            var authoredParent = content.parent;
            var entries = new List<Dictionary<string,object>>();
            VmAutomationUIToolkitElementUtility.QueryElements(scroll,"root","","",nameof(Label),"Content",
                false,1,entries);
            Assert.That(entries.Count,Is.EqualTo(1));
            Assert.That(VmAutomationUIToolkitElementUtility.GetElementByPath(scroll,(string)entries[0]["path"]),Is.SameAs(content));
            Assert.That(scroll.childCount,Is.EqualTo(1));
            Assert.That(content.parent,Is.SameAs(authoredParent));
            Assert.That(content.hierarchy.parent,Is.SameAs(scroll.contentContainer));
        }
    }
}
