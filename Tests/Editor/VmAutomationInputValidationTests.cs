using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAutomationInputValidationTests
    {
        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [Test]
        public async Task UnknownFieldCannotClaimRequestIdentity()
        {
            string requestId = Guid.NewGuid().ToString("N");
            var invalid = await VmAutomationExecutor.ExecuteAsync("editor/state",
                new Dictionary<string, object> { { "compilationMode", "debug" } },
                requestId: requestId, expectedProjectPath: ProjectRoot);
            Assert.That(invalid.Ok, Is.False);
            Assert.That(invalid.Error.Code, Is.EqualTo("invalid_arguments"));
            Assert.That(invalid.Error.Details["keyword"], Is.EqualTo("additionalProperties"));
            Assert.That(invalid.Error.Details["path"], Does.Contain("compilationMode"));
            var valid = await VmAutomationExecutor.ExecuteAsync("editor/state",
                requestId: requestId, expectedProjectPath: ProjectRoot);
            Assert.That(valid.Ok, Is.True, valid.Error?.Message);
        }

        [Test]
        public async Task WrongRefreshArgumentCannotAdmitWorkspaceJob()
        {
            bool activeBefore = VmAutomationWorkspaceJobRunner.HasActiveJob;
            var result = await VmAutomationExecutor.ExecuteAsync("asset/refresh",
                new Dictionary<string, object> { { "compilationMode", "debug" } },
                expectedProjectPath: ProjectRoot);
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo("invalid_arguments"));
            Assert.That(result.Error.Details["stage"], Is.EqualTo("input-validation"));
            Assert.That(VmAutomationWorkspaceJobRunner.HasActiveJob, Is.EqualTo(activeBefore));
        }

        [Test]
        public async Task SharedOwnerDoesNotCoerceNumberStrings()
        {
            var result = await VmAutomationExecutor.ExecuteAsync("code/policy-review",
                new Dictionary<string, object> { { "maxTypeLines", "1500" } },
                expectedProjectPath: ProjectRoot);
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo("invalid_arguments"));
            Assert.That(result.Error.Details["keyword"], Is.EqualTo("type"));
        }

        [Test]
        public async Task CanonicalBindingCannotHideANullJsonArgument()
        {
            var result = await VmAutomationExecutor.ExecuteAsync("editor/state",
                new Dictionary<string, object> { { "expectedProjectPath", null } },
                expectedProjectPath: ProjectRoot);
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo("invalid_arguments"));
            Assert.That(result.Error.Details["keyword"], Is.EqualTo("type"));
        }

        [Test]
        public void ReadonlyContractDeclaresTheBindingItAccepts()
        {
            Assert.That(VmAutomationCatalog.TryGetTool("editor/state", true, out var tool), Is.True);
            var schema = (Dictionary<string, object>)tool["inputSchema"];
            Assert.That(((Dictionary<string, object>)schema["properties"]).ContainsKey("expectedProjectPath"), Is.True);
            AssertValid(new Dictionary<string, object>(), schema);
            AssertValid(new Dictionary<string, object> { { "expectedProjectPath", ProjectRoot } }, schema);
            Assert.That(tool["errorCodes"], Does.Contain("invalid_arguments"));
            Assert.That(tool["errorCodes"], Does.Contain("input_validation_limit"));
        }

        [TestCase("text", 10, true)]
        [TestCase("text", "10", false)]
        [TestCase("text", 10.5, false)]
        [TestCase(10, 10, false)]
        public void TypesAreCheckedInsideClosedObjects(object text, object count, bool valid)
        {
            var schema = ObjectSchema(new Dictionary<string, object>
            {
                { "child", ObjectSchema(new Dictionary<string, object>
                    { { "name", TypeSchema("string") }, { "count", TypeSchema("integer") } }) }
            });
            Assert.That(Validate(new Dictionary<string, object>
                { { "child", new Dictionary<string, object> { { "name", text }, { "count", count } } } }, schema), Is.EqualTo(valid));
        }

        [Test]
        public void NestedUnknownFieldReportsItsLocation()
        {
            var schema = ObjectSchema(new Dictionary<string, object>
                { { "child", ObjectSchema(new Dictionary<string, object> { { "name", TypeSchema("string") } }) } });
            bool valid = VmAutomationInputValidator.TryValidate(new Dictionary<string, object>
                { { "child", new Dictionary<string, object> { { "nmae", "value" } } } }, schema,
                out string code, out _, out var details);
            Assert.That(valid, Is.False);
            Assert.That(code, Is.EqualTo("invalid_arguments"));
            Assert.That(details["path"], Is.EqualTo("$[\"child\"][\"nmae\"]"));
        }

        [TestCase("Debug", true)]
        [TestCase("debug", false)]
        [TestCase(0, false)]
        public void EnumCaseAndJsonTypeAreExact(object value, bool valid)
        {
            var schema = TypeSchema("string");
            schema["enum"] = new[] { "Debug", "Release" };
            Assert.That(Validate(value, schema), Is.EqualTo(valid));
        }

        [TestCase(true, true)]
        [TestCase(false, false)]
        [TestCase("true", false)]
        public void ConfirmationUsesAJsonBoolean(object value, bool valid)
        {
            var schema = TypeSchema("boolean");
            schema["const"] = true;
            Assert.That(Validate(value, schema), Is.EqualTo(valid));
        }

        [Test]
        public void RequiredBoundsAndUniqueArrayConstraintsAreEnforced()
        {
            var count = TypeSchema("number");
            count["minimum"] = 1;
            count["maximum"] = 4;
            AssertValid(1, count);
            Assert.That(Validate(0, count), Is.False);
            Assert.That(Validate(5, count), Is.False);
            var text = TypeSchema("string");
            text["minLength"] = 1;
            text["maxLength"] = 4;
            Assert.That(Validate("", text), Is.False);
            Assert.That(Validate("12345", text), Is.False);
            var array = TypeSchema("array");
            array["items"] = text;
            array["minItems"] = 1;
            array["maxItems"] = 2;
            array["uniqueItems"] = true;
            AssertValid(new[] { "one", "two" }, array);
            Assert.That(Validate(new[] { "one", "one" }, array), Is.False);
            Assert.That(Validate(Array.Empty<string>(), array), Is.False);
            Assert.That(Validate(new[] { "one", "two", "more" }, array), Is.False);
            var required = ObjectSchema(new Dictionary<string, object> { { "text", text } });
            required["required"] = new[] { "text" };
            Assert.That(Validate(new Dictionary<string, object>(), required), Is.False);
        }

        [Test]
        public void NumericIdentityRetainsPrecisionAndIgnoresDecimalScale()
        {
            Assert.That(VmAutomationCanonicalJson.ComputeSha256(1d),
                Is.Not.EqualTo(VmAutomationCanonicalJson.ComputeSha256(1.0000000000000002d)));
            Assert.That(VmAutomationCanonicalJson.ComputeSha256(1.00m),
                Is.EqualTo(VmAutomationCanonicalJson.ComputeSha256(1L)));
            var array = TypeSchema("array");
            array["items"] = TypeSchema("number");
            array["uniqueItems"] = true;
            Assert.That(Validate(new object[] { 1L, 1.00m }, array), Is.False);
            AssertValid(new[] { 1d, 1.0000000000000002d }, array);
        }

        [Test]
        public void CombinatorsPreserveSelectorAndDiscriminatorSemantics()
        {
            var first = ObjectSchema(new Dictionary<string, object> { { "kind", TypeSchema("string") } });
            ((Dictionary<string, object>)((Dictionary<string, object>)first["properties"])["kind"])["const"] = "first";
            first["required"] = new[] { "kind" };
            var second = ObjectSchema(new Dictionary<string, object> { { "value", TypeSchema("number") } });
            second["required"] = new[] { "value" };
            var union = new Dictionary<string, object> { { "oneOf", new[] { first, second } } };
            AssertValid(new Dictionary<string, object> { { "kind", "first" } }, union);
            AssertValid(new Dictionary<string, object> { { "value", 2 } }, union);
            Assert.That(Validate(new Dictionary<string, object> { { "kind", "second" } }, union), Is.False);
            var alternatives = new Dictionary<string, object>
            {
                { "anyOf", new[] { new Dictionary<string, object> { { "required", new[] { "a" } } },
                    new Dictionary<string, object> { { "required", new[] { "b" } } } } },
                { "not", new Dictionary<string, object> { { "required", new[] { "a", "b" } } } }
            };
            AssertValid(new Dictionary<string, object> { { "a", 1 } }, alternatives);
            Assert.That(Validate(new Dictionary<string, object> { { "a", 1 }, { "b", 2 } }, alternatives), Is.False);
            alternatives["allOf"] = new[] { new Dictionary<string, object> { { "required", new[] { "c" } } } };
            Assert.That(Validate(new Dictionary<string, object> { { "a", 1 } }, alternatives), Is.False);
        }

        [Test]
        public void RecursiveJsonValuesAndTypedMapsRemainAccepted()
        {
            var schema = VmJsonContract.CreateSchema(typeof(Dictionary<string, object>));
            AssertValid(new Dictionary<string, object>
            {
                { "nested", new object[] { null, true, 2L, 2.5, "text", new Dictionary<string, object> { { "other", false } } } }
            }, schema);
            var map = VmJsonContract.CreateSchema(typeof(Dictionary<string, string>));
            AssertValid(new Dictionary<string, object> { { "unrestricted-key", "value" } }, map);
            Assert.That(Validate(new Dictionary<string, object> { { "unrestricted-key", 1 } }, map), Is.False);
        }

        [Test]
        public void NullableEnumsPublishAndAcceptNull()
        {
            var schema = VmJsonContract.CreateSchema(typeof(Optimization?));
            AssertValid(null, schema);
            AssertValid("Debug", schema);
            Assert.That(Validate("debug", schema), Is.False);
        }

        [Test]
        public void FlagPatternAndEvaluatorCapacityAreExplicit()
        {
            var schema = VmJsonContract.CreateSchema(typeof(Options));
            AssertValid("First, Second", schema);
            Assert.That(Validate("first, Second", schema), Is.False);
            var array = TypeSchema("array");
            array["items"] = TypeSchema("number");
            bool valid = VmAutomationInputValidator.TryValidate(
                Enumerable.Repeat(1, VmAutomationInputValidator.MaximumWorkUnits).ToArray(), array,
                out string code, out _, out var details);
            Assert.That(valid, Is.False);
            Assert.That(code, Is.EqualTo("input_validation_limit"));
            Assert.That(details["keyword"], Is.EqualTo("workUnits"));
        }

        private static bool Validate(object value, Dictionary<string, object> schema) =>
            VmAutomationInputValidator.TryValidate(value, schema, out _, out _, out _);

        private static void AssertValid(object value, Dictionary<string, object> schema)
        {
            Assert.That(VmAutomationInputValidator.TryValidate(value, schema, out _, out string message, out _), Is.True, message);
        }

        private static Dictionary<string, object> TypeSchema(string type) =>
            new Dictionary<string, object> { { "type", type } };

        private static Dictionary<string, object> ObjectSchema(Dictionary<string, object> properties) =>
            new Dictionary<string, object> { { "type", "object" }, { "properties", properties }, { "additionalProperties", false } };

        private enum Optimization { Debug, Release }
        [Flags]
        private enum Options { First = 1, Second = 2 }
    }
}
