using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmUIToolkitOpacityTests
    {
        private sealed class OpacityWindow : EditorWindow { }

        [UnityTest]
        public IEnumerator StyleSnapshotsMeasureAncestorOpacityAndCurrentParentOwnership()
        {
            var window = ScriptableObject.CreateInstance<OpacityWindow>();
            var ancestor = new VisualElement();
            var parent = new VisualElement();
            var child = new VisualElement();
            ancestor.style.opacity = .8f;
            parent.style.opacity = .5f;
            child.style.opacity = 1;
            window.rootVisualElement.Add(ancestor);
            ancestor.Add(parent);
            parent.Add(child);
            window.Show();
            try
            {
                yield return null;
                yield return null;
                AssertOpacity(child, 1, .4f);
                parent.style.opacity = 0;
                yield return null;
                AssertOpacity(child, 1, 0);
                parent.style.opacity = 1;
                yield return null;
                AssertOpacity(child, 1, .8f);
                window.rootVisualElement.Add(child);
                yield return null;
                AssertOpacity(child, 1, 1);
            }
            finally
            {
                window.Close();
            }
        }

        private static void AssertOpacity(VisualElement element, float local, float effective)
        {
            var info = VmAutomationUIToolkitElementUtility.BuildElementInfo(element, "root/0", true);
            var style = (Dictionary<string, object>)info["resolvedStyle"];
            Assert.That((float)style["opacity"], Is.EqualTo(local));
            Assert.That((float)style["effectiveOpacity"], Is.EqualTo(effective).Within(.00001f));
            VmAutomationGeneratedRouteContracts.TryGetOutput("uitoolkit/runtime-style", out var output);
            var properties = (Dictionary<string, object>)output["properties"];
            var styleSchema = (Dictionary<string, object>)properties["resolvedStyle"];
            var styleProperties = (Dictionary<string, object>)styleSchema["properties"];
            Assert.That(styleProperties.ContainsKey("effectiveOpacity"), Is.True);
        }
    }
}
