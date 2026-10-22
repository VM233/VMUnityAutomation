using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmAutomationResponseJsonTokensTests
    {
        private static readonly Dictionary<string, object> Schema = new Dictionary<string, object>
        {
            { "type", "object" },
            { "properties", new Dictionary<string, object> { { "success", new Dictionary<string, object>() } } }
        };

        [Test]
        public void NestedSnapshotRetainsLogicalValuesThroughProjectEnvelope()
        {
            var snapshot = JObject.Parse("{\"health\":0.4,\"buffs\":[{\"id\":\"regen\",\"level\":1}],\"shield\":null}");
            var product = new { success = true, before = snapshot, after = new JArray(snapshot.DeepClone()) };
            var envelope = new Dictionary<string, object>
            {
                { "success", true }, { "toolName", "fixture/json-snapshot" }, { "result", product }
            };

            object actual = VmAutomationResponse.CompactForTransport(envelope, Schema);
            Assert.That(JToken.DeepEquals(JToken.FromObject(actual), JToken.FromObject(product)), Is.True);
        }

        [TestCase("{}")]
        [TestCase("[]")]
        [TestCase("[1,2.5,true,null,\"text\",{},[]]")]
        [TestCase("{\"empty\":{},\"items\":[],\"$unityStruct\":\"domain-owned\"}")]
        public void RootDomUsesItsJsonShape(string json)
        {
            JToken token = JToken.Parse(json);
            object actual = VmAutomationResponse.CompactForTransport(token, Schema);
            Assert.That(JToken.DeepEquals(JToken.FromObject(actual), token), Is.True);
        }

        [Test]
        public void AttachedObjectDoesNotExposeNavigationMetadata()
        {
            var child = new JObject { { "value", 7 } };
            var parent = new JObject { { "child", child } };
            var actual = (Dictionary<string, object>)VmAutomationResponse.CompactForTransport(child, Schema);
            Assert.That(child.Parent, Is.Not.Null);
            Assert.That(parent["child"], Is.SameAs(child));
            Assert.That(actual.Keys, Is.EquivalentTo(new[] { "value" }));
            Assert.That(actual["value"], Is.EqualTo(7));
        }

        [Test]
        public void JsonErrorUsesTheSameErrorEnvelopeContract()
        {
            var error = JObject.Parse("{\"success\":false,\"error\":\"Exact cause.\",\"errorCode\":\"fixture_error\",\"retryable\":true}");
            Assert.That(VmAutomationResponse.TryGetError(error, out string message, out string code,
                out bool retryable), Is.True);
            Assert.That(message, Is.EqualTo("Exact cause."));
            Assert.That(code, Is.EqualTo("fixture_error"));
            Assert.That(retryable, Is.True);
            Assert.That(VmAutomationResponse.NormalizeError(error)["errorCode"], Is.EqualTo(code));
        }

        [Test]
        public void ScalarErrorProbeDoesNotReflectItsParent()
        {
            var parent = new JObject { { "value", 7 } };
            Assert.That(VmAutomationResponse.ToDictionary(parent["value"]), Is.Null);
            Assert.That(VmAutomationResponse.TryGetError(parent["value"], out _, out _, out _), Is.False);
        }

        [Test]
        public void DeepDomConversionUsesLogicalWorkInsteadOfCallDepth()
        {
            JToken token = new JValue(5);
            for (int depth = 0; depth < 256; depth++)
                token = new JObject { { "child", token } };

            object current = VmAutomationResponse.CompactForTransport(token, Schema);
            for (int depth = 0; depth < 256; depth++)
                current = ((Dictionary<string, object>)current)["child"];
            Assert.That(current, Is.EqualTo(5));
        }

        [Test]
        public void NonJsonDomNodesAreRejectedAtTheResponseOwner()
        {
            Assert.Throws<ArgumentException>(() =>
                VmAutomationResponse.CompactForTransport(new JConstructor("not-json"), Schema));
        }
    }
}
