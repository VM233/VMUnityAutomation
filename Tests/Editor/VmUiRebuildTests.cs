using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmUiRebuildTests
    {
        [Test]
        public void InvalidIdentityDoesNotCreateSceneObjects()
        {
            var before = VmAutomationObjectSearch.Find<GameObject>(true);
            var error = Assert.Throws<VmProjectToolException>(() => new VmUiRebuildTool().Execute(
                new VmUiRebuildRequest { RootInstanceId = "0" }));
            Assert.That(error.ErrorCode, Is.EqualTo("ui_root_not_found"));
            CollectionAssert.AreEquivalent(before, VmAutomationObjectSearch.Find<GameObject>(true));
        }

        [Test]
        public void RebuildReplacesStaleGeneratorContentAndPreservesSerializedText()
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("UiRebuildFixture", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(root, preview);
                var child = new GameObject("Label", typeof(RectTransform), typeof(Text));
                child.transform.SetParent(root.transform, false);
                var text = child.GetComponent<Text>();
#if UNITY_2022_2_OR_NEWER
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
                text.fontSize = 20;
                text.rectTransform.sizeDelta = new Vector2(256, 64);
                text.text = "FRESH";
                string serialized = EditorJsonUtility.ToJson(text);
                text.cachedTextGenerator.Populate("OLD", text.GetGenerationSettings(new Vector2(256, 64)));
                Assert.That(text.cachedTextGenerator.characterCountVisible, Is.EqualTo(3));
                var result = new VmUiRebuildTool().Execute(new VmUiRebuildRequest
                {
                    RootInstanceId = VmObjectId.Get(root)
                });
                Assert.That(result.NodeCount, Is.EqualTo(2));
                Assert.That(result.ActiveTextsPrepared, Is.EqualTo(1));
                Assert.That(result.ActiveGraphicsRebuilt, Is.EqualTo(1));
                Assert.That(text.cachedTextGenerator.characterCountVisible, Is.EqualTo(5));
                Assert.That(EditorJsonUtility.ToJson(text), Is.EqualTo(serialized));
                Assert.That(root.transform.childCount, Is.EqualTo(1));
                Assert.That(child.activeSelf, Is.True);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        [Test]
        public void RebuildMetadataOwnsViewChangesWithoutAssetWrites()
        {
            var attribute = (VmProjectToolAttribute)System.Attribute.GetCustomAttribute(
                typeof(VmUiRebuildTool), typeof(VmProjectToolAttribute));
            var descriptor = VmProjectToolDescriptor.FromType(attribute, typeof(VmUiRebuildTool));
            Assert.That(descriptor.ValidationError, Is.Null.Or.Empty);
            var effects = ((IEnumerable)descriptor.ToDetailDictionary()["sideEffects"]).Cast<string>().ToArray();
            Assert.That(effects, Does.Contain("changesEditorView"));
            Assert.That(effects, Does.Not.Contain("writesAssets"));
            Assert.That(effects, Does.Not.Contain("writesProjectFiles"));
        }
    }
}
