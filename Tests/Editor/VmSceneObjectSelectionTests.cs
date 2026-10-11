using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.SceneManagement;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmSceneObjectSelectionTests
    {
        private Scene original, first, second;
        private string prefix, assetFolder;

        [SetUp]
        public void SetUp()
        {
            original = SceneManager.GetActiveScene();
            first = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            second = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(first);
            prefix = "Selection " + Guid.NewGuid().ToString("N");
        }

        [TearDown]
        public void TearDown()
        {
            SceneManager.SetActiveScene(original);
            EditorSceneManager.CloseScene(second, true);
            EditorSceneManager.CloseScene(first, true);
            if (assetFolder != null)
            {
                Assert.That(AssetDatabase.DeleteAsset(assetFolder), Is.True);
                assetFolder = null;
            }
        }

        private GameObject Create(string suffix, Scene scene, Transform parent = null)
        {
            var value = new GameObject(prefix + suffix);
            SceneManager.MoveGameObjectToScene(value, scene);
            if (parent != null) value.transform.SetParent(parent);
            return value;
        }

        [Test]
        public void InactiveRootAndChildResolveThroughTheSameSelector()
        {
            var root = Create(" Root", first);
            var child = Create(" Child", first, root.transform);
            root.SetActive(false);
            Assert.That(VmAutomationGameObjectCommands.FindGameObject(root.name), Is.SameAs(root));
            Assert.That(VmAutomationGameObjectCommands.FindGameObject("/" + root.name + "/" + child.name), Is.SameAs(child));
            Assert.That(VmAutomationObjectSearch.Find<GameObject>(true), Does.Contain(child));
        }

        [Test]
        public void DuplicateNamesRejectAndExactIdentityRemainsUsable()
        {
            var left = Create(" Duplicate", first);
            var right = Create(" Duplicate", second);
            left.SetActive(false);
            Assert.Throws<ArgumentException>(() => VmAutomationGameObjectCommands.FindGameObject(left.name));
            Assert.That(VmAutomationGameObjectCommands.FindGameObject(new Dictionary<string, object>
                { { "instanceId", VmObjectId.Get(right) } }), Is.SameAs(right));
            Assert.That(VmAutomationGameObjectCommands.FindGameObject(left.name, first), Is.SameAs(left));
        }

        [Test]
        public void HierarchyParentBelongsToTheReportedActiveScene()
        {
            var left = Create(" Same Path", first);
            var right = Create(" Same Path", second);
            left.SetActive(false);
            right.SetActive(false);
            foreach (var scene in new[] { first, second })
            {
                SceneManager.SetActiveScene(scene);
                var result = (Dictionary<string, object>)VmAutomationSceneCommands.GetHierarchy(
                    new Dictionary<string, object> { { "parentPath", left.name }, { "maxNodes", 8 } });
                Assert.That(result.ContainsKey("error"), Is.False);
                Assert.That(result["scene"], Is.EqualTo(scene.name));
                Assert.That(result["totalSceneObjects"], Is.EqualTo(1));
            }
        }

        [Test]
        public void CreateResolvesAnInactiveParentAndMissingParentCannotCreateAnObject()
        {
            var parent = Create(" Parent", first);
            parent.SetActive(false);
            var result = (Dictionary<string, object>)VmAutomationGameObjectCommands.Create(
                new Dictionary<string, object> { { "name", prefix + " Created" }, { "parent", parent.name } });
            var child = VmObjectId.ToObject(result["instanceId"]) as GameObject;
            Assert.That(child.transform.parent, Is.SameAs(parent.transform));
            int before = first.GetRootGameObjects().Length;
            var rejected = VmAutomationGameObjectCommands.Create(new Dictionary<string, object>
                { { "name", prefix + " Forbidden" }, { "parent", prefix + " Missing" } });
            Assert.That(rejected.GetType().GetProperty("error").GetValue(rejected), Does.Contain("not found"));
            Assert.That(first.GetRootGameObjects().Length, Is.EqualTo(before));
            Assert.That(parent.transform.childCount, Is.EqualTo(1));
        }

        [Test]
        public void MissingReparentCannotDetachTheExistingChild()
        {
            var parent = Create(" Parent", first);
            var child = Create(" Child", first, parent.transform);
            var replacement = Create(" Replacement", first);
            replacement.SetActive(false);
            var rejected = VmAutomationGameObjectCommands.Reparent(new Dictionary<string, object>
                { { "instanceId", VmObjectId.Get(child) }, { "newParent", prefix + " Missing" } });
            Assert.That(rejected.GetType().GetProperty("error").GetValue(rejected), Does.Contain("not found"));
            Assert.That(child.transform.parent, Is.SameAs(parent.transform));
            VmAutomationGameObjectCommands.Reparent(new Dictionary<string, object>
                { { "instanceId", VmObjectId.Get(child) }, { "newParent", replacement.name } });
            Assert.That(child.transform.parent, Is.SameAs(replacement.transform));
        }

        [Test]
        public void InactiveConstraintSourceResolvesAndMissingSourceCannotAddAComponent()
        {
            var target = Create(" Target", first);
            var source = Create(" Source", first);
            target.SetActive(false);
            source.SetActive(false);
            VmAutomationConstraintCommands.AddConstraint(new Dictionary<string, object>
                { { "path", target.name }, { "type", "position" }, { "source", source.name } });
            var constraint = target.GetComponent<PositionConstraint>();
            Assert.That(constraint.GetSource(0).sourceTransform, Is.SameAs(source.transform));
            var rejected = VmAutomationConstraintCommands.AddConstraint(new Dictionary<string, object>
                { { "path", target.name }, { "type", "rotation" }, { "source", prefix + " Missing" } });
            Assert.That(rejected.GetType().GetProperty("error").GetValue(rejected), Does.Contain("not found"));
            Assert.That(target.GetComponent<RotationConstraint>(), Is.Null);
        }

        [Test]
        public void SerializedComponentReferenceFindsAnInactiveNativeSource()
        {
            var target = Create(" Joint", first);
            var source = Create(" Body", first);
            var joint = target.AddComponent<HingeJoint>();
            var body = source.AddComponent<Rigidbody>();
            source.SetActive(false);
            VmAutomationComponentCommands.SetReferences(new Dictionary<string, object>
            {
                { "instanceId", VmObjectId.Get(target) }, { "componentType", typeof(HingeJoint).FullName },
                { "references", new List<object> { new Dictionary<string, object>
                    {
                        { "propertyName", "m_ConnectedBody" }, { "referenceGameObject", source.name },
                        { "referenceComponentType", typeof(Rigidbody).FullName }
                    } } }
            });
            Assert.That(joint.connectedBody, Is.SameAs(body));
        }

        [Test]
        public void PrefabParentAdmissionPrecedesInstantiation()
        {
            string folderName = "SelectionTest" + Guid.NewGuid().ToString("N");
            Assert.That(AssetDatabase.CreateFolder("Assets", folderName), Is.Not.Empty);
            assetFolder = "Assets/" + folderName;
            var source = Create(" Prefab Source", first);
            string path = assetFolder + "/Selection.prefab";
            PrefabUtility.SaveAsPrefabAsset(source, path);
            var parent = Create(" Parent", first);
            parent.SetActive(false);
            int before = first.GetRootGameObjects().Length;
            var rejected = VmAutomationAssetCommands.InstantiatePrefab(new Dictionary<string, object>
                { { "prefabPath", path }, { "parent", prefix + " Missing" } });
            Assert.That(rejected.GetType().GetProperty("error").GetValue(rejected), Does.Contain("not found"));
            Assert.That(first.GetRootGameObjects().Length, Is.EqualTo(before));
            var result = (Dictionary<string, object>)VmAutomationAssetCommands.InstantiatePrefab(
                new Dictionary<string, object> { { "prefabPath", path }, { "parent", parent.name } });
            var instance = VmObjectId.ToObject(result["instanceId"]) as GameObject;
            Assert.That(instance.transform.parent, Is.SameAs(parent.transform));
        }

        [Test]
        public void LodInspectionIncludesTheInactiveOwner()
        {
            var target = Create(" Lod", first);
            target.AddComponent<LODGroup>();
            target.SetActive(false);
            var result = (Dictionary<string, object>)VmAutomationConstraintCommands.GetLODGroupInfo(
                new Dictionary<string, object> { { "path", target.name } });
            Assert.That(result["gameObject"], Is.EqualTo(target.name));
        }
    }
}
