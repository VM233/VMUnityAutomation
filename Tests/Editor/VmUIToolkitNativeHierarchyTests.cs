using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmUIToolkitNativeHierarchyTests
    {
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
            var entries = new List<Dictionary<string,object>>();
            VmAutomationUIToolkitElementUtility.QueryElements(scroll,"root","","",nameof(Label),"Content",
                false,1,entries);
            Assert.That(entries.Count,Is.EqualTo(1));
            Assert.That(VmAutomationUIToolkitElementUtility.GetElementByPath(scroll,(string)entries[0]["path"]),Is.SameAs(content));
            Assert.That(scroll.childCount,Is.EqualTo(1));
            Assert.That(content.parent,Is.SameAs(scroll.contentContainer));
        }
    }
}
