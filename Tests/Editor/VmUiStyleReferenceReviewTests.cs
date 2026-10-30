using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmUiStyleReferenceReviewTests
    {
        [Test]
        public void DifferentObjectWithSameContentCannotStandInForCachedReference()
        {
            var first = new Texture2D(1, 1);
            var second = new Texture2D(1, 1);
            try
            {
                var state = new VmUiStyleReferenceState(first);
                Assert.That(state.AgreesWith(new VmUiStyleReferenceState(first)), Is.True);
                Assert.That(state.AgreesWith(new VmUiStyleReferenceState(second)), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void DestroyedCachedWrapperIsDistinctFromBothLiveAndClrNullReferences()
        {
            var texture = new Texture2D(1, 1);
            var live = new VmUiStyleReferenceState(texture);
            Object.DestroyImmediate(texture);
            var destroyed = new VmUiStyleReferenceState(texture);
            Assert.That(destroyed.ClrNull, Is.False);
            Assert.That(destroyed.Alive, Is.False);
            Assert.That(destroyed.AgreesWith(live), Is.False);
            Assert.That(destroyed.AgreesWith(new VmUiStyleReferenceState(null)), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NativeSerializationOfLiveReferenceMatchesWithoutReplacingCachedArray(bool includeMatches)
        {
            var sheet = ScriptableObject.CreateInstance<StyleSheet>();
            var texture = new Texture2D(1, 1);
            var references = new Object[] { texture };
            var field = typeof(StyleSheet).GetField("assets", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(sheet, references);
            try
            {
                var result = VmUiStyleReferenceReviewTool.ReviewSheet("in-memory", sheet, includeMatches);
                Assert.That(result["mismatchCount"], Is.EqualTo(0));
                Assert.That(result["managedReferenceCount"], Is.EqualTo(1));
                Assert.That(result["serializedReferenceCount"], Is.EqualTo(1));
                Assert.That((List<object>)result["references"], Has.Count.EqualTo(includeMatches ? 1 : 0));
                Assert.That(field.GetValue(sheet), Is.SameAs(references));
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(sheet);
            }
        }

        [Test]
        public void EmptyInlineSheetHasCompleteZeroReferenceResult()
        {
            var result = VmUiStyleReferenceReviewTool.ReviewSheet("empty-uxml", null, true);
            Assert.That(result["sheetPresent"], Is.False);
            Assert.That(result["countsMatch"], Is.True);
            Assert.That(result["mismatchCount"], Is.EqualTo(0));
        }

        [TestCase(0)]
        [TestCase(256)]
        public void FrozenReferenceBoundaryIsAdmitted(int count) =>
            Assert.DoesNotThrow(() => VmUiStyleReferenceReviewTool.ValidateReferenceCount(count));

        [TestCase(-1)]
        [TestCase(257)]
        public void OutOfBudgetReferencesRejectRatherThanTruncate(int count) =>
            Assert.Throws<VmProjectToolException>(() => VmUiStyleReferenceReviewTool.ValidateReferenceCount(count));

        [Test]
        public void MissingPathsRejectBeforeAssetLookup() =>
            Assert.Throws<VmProjectToolException>(() => new VmUiStyleReferenceReviewTool().Execute(new Dictionary<string, object>()));

        [TestCase("../Assets/skin.uss")]
        [TestCase("Assets/../skin.uss")]
        [TestCase("Assets/skin.asset")]
        public void NonStyleAssetOrTraversalRejectsBeforeAssetLookup(string path) =>
            Assert.Throws<VmProjectToolException>(() => new VmUiStyleReferenceReviewTool().Execute(
                new Dictionary<string, object> { ["assetPaths"] = new[] { path } }));
    }
}
