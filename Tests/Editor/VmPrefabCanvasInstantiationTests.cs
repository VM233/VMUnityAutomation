using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmPrefabCanvasInstantiationTests
    {
        [Test]
        public void ParentedUiInstantiationPreservesStretchAndHonorsExplicitWorldPosition()
        {
            string folder = "Assets/VmCanvasInstantiation_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            var parent = new GameObject("CanvasParent_" + Guid.NewGuid().ToString("N"), typeof(RectTransform));
            var template = new GameObject("StretchedPopup", typeof(RectTransform));
            GameObject instance = null;
            try
            {
                var canvas = (RectTransform)parent.transform;
                canvas.sizeDelta = new Vector2(800, 600);
                canvas.position = new Vector3(400, 300, 0);
                var root = (RectTransform)template.transform;
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.sizeDelta = Vector2.zero;
                string path = folder + "/Popup.prefab";
                PrefabUtility.SaveAsPrefabAsset(template, path);
                var result = (Dictionary<string, object>)VmAutomationAssetCommands.InstantiatePrefab(
                    new Dictionary<string, object> { ["prefabPath"] = path, ["parent"] = parent.name });
                instance = (GameObject)VmObjectId.ToObject((string)result["instanceId"]);
                var rect = (RectTransform)instance.transform;
                Assert.That(rect.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(rect.sizeDelta, Is.EqualTo(Vector2.zero));
                Assert.That(rect.rect.size, Is.EqualTo(new Vector2(800, 600)));
                UnityEngine.Object.DestroyImmediate(instance);
                result = (Dictionary<string, object>)VmAutomationAssetCommands.InstantiatePrefab(
                    new Dictionary<string, object> { ["prefabPath"] = path, ["parent"] = parent.name,
                        ["position"] = new Dictionary<string, object> { ["x"] = 12, ["y"] = 34, ["z"] = 0 } });
                instance = (GameObject)VmObjectId.ToObject((string)result["instanceId"]);
                Assert.That(instance.transform.position, Is.EqualTo(new Vector3(12, 34, 0)));
                Assert.That(((RectTransform)instance.transform).sizeDelta, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(template);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
