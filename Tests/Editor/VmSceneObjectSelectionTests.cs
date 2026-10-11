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
        private UnityEngine.Object[] selected;

        [SetUp]
        public void SetUp()
        {
            original = SceneManager.GetActiveScene();
            selected = Selection.objects;
            assetFolder = "Assets/SelectionTest" + Guid.NewGuid().ToString("N");
            Assert.That(AssetDatabase.CreateFolder("Assets", assetFolder.Substring(7)), Is.Not.Empty);
            first = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Assert.That(EditorSceneManager.SaveScene(first, assetFolder + "/First.unity"), Is.True);
            second = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Assert.That(EditorSceneManager.SaveScene(second, assetFolder + "/Second.unity"), Is.True);
            SceneManager.SetActiveScene(first);
            prefix = "Selection " + Guid.NewGuid().ToString("N");
        }

        [TearDown]
        public void TearDown()
        {
            SceneManager.SetActiveScene(original);
            Selection.objects = selected;
            if (second.IsValid() && second.isLoaded) Assert.That(EditorSceneManager.CloseScene(second, true), Is.True);
            if (first.IsValid() && first.isLoaded) Assert.That(EditorSceneManager.CloseScene(first, true), Is.True);
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
                var root = (Dictionary<string, object>)((List<object>)result["hierarchy"])[0];
                Assert.That(root["instanceId"], Is.EqualTo(VmObjectId.Get(scene == first ? left : right)));
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

        [Test]
        public void ParticleParentAdmissionCannotCreateAnUnparentedSystem()
        {
            var parent = Create(" Particle Parent", first);
            parent.SetActive(false);
            int before = first.GetRootGameObjects().Length;
            var rejected = VmAutomationParticleCommands.CreateParticleSystem(new Dictionary<string, object>
                { { "name", prefix + " Forbidden Particle" }, { "parent", prefix + " Missing" } });
            Assert.That(rejected.GetType().GetProperty("error").GetValue(rejected), Does.Contain("not found"));
            Assert.That(first.GetRootGameObjects().Length, Is.EqualTo(before));
            var result = (Dictionary<string, object>)VmAutomationParticleCommands.CreateParticleSystem(
                new Dictionary<string, object> { { "name", prefix + " Particle" }, { "parent", parent.name }, { "playOnAwake", false } });
            var child = VmObjectId.ToObject(result["instanceId"]) as GameObject;
            Assert.That(child.transform.parent, Is.SameAs(parent.transform));
            var info = (Dictionary<string, object>)VmAutomationParticleCommands.GetParticleSystemInfo(
                new Dictionary<string, object> { { "path", parent.name + "/" + child.name } });
            Assert.That(info["instanceId"], Is.EqualTo(VmObjectId.Get(child)));
        }

        [Test]
        public void MissingUguiParentCannotUseAnUnrelatedCanvas()
        {
            var canvas = Create(" Unrelated Canvas", first);
            canvas.AddComponent<Canvas>();
            var parent = Create(" Ui Parent", first);
            parent.AddComponent<RectTransform>();
            parent.SetActive(false);
            var rejected = VmAutomationUICommands.CreateUIElement(new Dictionary<string, object>
                { { "type", "panel" }, { "name", prefix + " Forbidden Panel" }, { "parent", prefix + " Missing" } });
            Assert.That(rejected.GetType().GetProperty("error").GetValue(rejected), Does.Contain("not found"));
            Assert.That(canvas.transform.childCount, Is.Zero);
            var result = (Dictionary<string, object>)VmAutomationUICommands.CreateUIElement(new Dictionary<string, object>
                { { "type", "panel" }, { "name", prefix + " Panel" }, { "parent", parent.name } });
            var child = VmObjectId.ToObject(result["instanceId"]) as GameObject;
            Assert.That(child.transform.parent, Is.SameAs(parent.transform));
            var image = (Dictionary<string, object>)VmAutomationUICommands.SetUIImage(new Dictionary<string, object>
                { { "path", parent.name + "/" + child.name }, { "color", new Dictionary<string, object> { { "a", .25 } } } });
            Assert.That(image["success"], Is.True);
            Component imageComponent = null;
            foreach (var component in child.GetComponents<Component>())
                if (component.GetType().Name == "Image") imageComponent = component;
            Assert.That(new SerializedObject(imageComponent).FindProperty("m_Color").colorValue.a, Is.EqualTo(.25f));
        }

        [Test]
        public void GraphicsAndNavigationConsumeTheInactiveOwner()
        {
            var target = Create(" Native Owner", first);
            target.SetActive(false);
            target.AddComponent<MeshRenderer>();
            target.AddComponent<Light>();
            var renderer = (Dictionary<string, object>)VmAutomationGraphicsCommands.GetRendererInfo(
                new Dictionary<string, object> { { "gameObjectPath", target.name } });
            Assert.That(renderer["gameObjectPath"], Is.EqualTo(target.name));
            var light = (Dictionary<string, object>)VmAutomationGraphicsCommands.GetLightingSummary(
                new Dictionary<string, object> { { "lightName", target.name } });
            Assert.That(light["lightCount"], Is.EqualTo(1));
            var actualLight = ((List<Dictionary<string, object>>)light["lights"])[0];
            Assert.That(actualLight["name"], Is.EqualTo(target.name));
            Assert.That(actualLight["gameObjectActive"], Is.False);
            VmAutomationNavigationCommands.AddNavMeshObstacle(new Dictionary<string, object> { { "path", target.name } });
            Assert.That(target.GetComponent<UnityEngine.AI.NavMeshObstacle>(), Is.Not.Null);
        }

        [Test]
        public void SerializedTargetAndSelectionRetainTheInactiveIdentity()
        {
            var target = Create(" Serialized Owner", first);
            target.SetActive(false);
            var serialized = (Dictionary<string, object>)VmAutomationSerializedObjectCommands.Get(new Dictionary<string, object>
                { { "gameObjectPath", target.name }, { "propertyPath", "m_Name" } });
            Assert.That(serialized.ContainsKey("property"), Is.True);
            VmAutomationSelectionCommands.SetSelection(new Dictionary<string, object> { { "path", target.name } });
            Assert.That(Selection.objects, Is.EqualTo(new UnityEngine.Object[] { target }));
        }

        [Test]
        public void TerrainNameSelectionIncludesTheInactiveComponent()
        {
            var target = Create(" Terrain", first);
            target.SetActive(false);
            var terrain = target.AddComponent<Terrain>();
            Assert.That(VmAutomationTerrainCommandUtility.FindTerrain(new Dictionary<string, object>
                { { "name", target.name } }), Is.SameAs(terrain));
            Assert.That(VmAutomationTerrainCommandUtility.FindTerrainByName(target.name), Is.SameAs(terrain));
        }
    }
}
