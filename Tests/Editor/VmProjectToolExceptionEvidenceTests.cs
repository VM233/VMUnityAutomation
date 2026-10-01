using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    public sealed class VmProjectToolExceptionEvidenceTests
    {
        private const string ToolName = "tests/cli-exception-evidence";
        internal const string InputSchema =
            "{\"type\":\"object\",\"properties\":{\"expected\":{\"type\":\"boolean\"}}," +
            "\"required\":[\"expected\"],\"additionalProperties\":false}";
        internal const string OutputSchema =
            "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}";

        [VmProjectTool(ToolName, ReadOnly = true,
            Description = "Test fixture for the project-tool exception boundary.",
            InputSchemaJson = InputSchema, OutputSchemaJson = OutputSchema,
            ErrorCodes = new[] { "expected_fixture_rejection" },
            SideEffects = VmProjectToolSideEffect.ReadsProjectState)]
        public static object Fixture(Dictionary<string, object> arguments)
        {
            if ((bool)arguments["expected"])
                throw new VmProjectToolException("expected_fixture_rejection", "Expected rejection.");
            throw new InvalidOperationException("CLI exception evidence fixture.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task InvocationPublishesExpectedAndUnexpectedFailuresSeparately(bool expected)
        {
            VmProjectToolRegistry.ResetCacheForTests();
            Assert.That(VmAutomationCatalog.TryGetTool(
                VmAutomationCatalog.ProjectToolNameToToolName(ToolName), true, out var metadata), Is.True);
            Assert.That((IEnumerable)metadata["errorCodes"],
                Does.Contain(VmProjectToolRegistry.UnexpectedExceptionErrorCode));
            if (!expected)
                LogAssert.Expect(LogType.Exception,
                    new Regex("InvalidOperationException: CLI exception evidence fixture"));
            var result = await VmAutomationExecutor.ExecuteAsync(
                VmAutomationCatalog.ProjectToolNameToToolName(ToolName),
                new Dictionary<string, object> { { "expected", expected } });
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error.Details["toolName"], Is.EqualTo(ToolName));
            if (expected)
            {
                Assert.That(result.Error.Code, Is.EqualTo("expected_fixture_rejection"));
                Assert.That(result.Error.Details.ContainsKey("exceptionType"), Is.False);
                LogAssert.NoUnexpectedReceived();
            }
            else
            {
                Assert.That(result.Error.Code, Is.EqualTo("project_tool_exception"));
                Assert.That(result.Error.Details["exceptionType"],
                    Is.EqualTo(typeof(InvalidOperationException).FullName));
                Assert.That(result.Error.Details["stackTrace"].ToString(), Does.Contain("Fixture"));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CooperativeStepPublishesTheSameExceptionEvidence(bool expected)
        {
            VmProjectToolRegistry.ResetCacheForTests();
            Assert.That(VmAutomationCatalog.TryGetTool(
                VmAutomationCatalog.ProjectToolNameToToolName(VmProjectToolExceptionEvidenceJobFixture.ToolName),
                true, out var metadata), Is.True);
            Assert.That((IEnumerable)metadata["errorCodes"],
                Does.Contain(VmProjectToolRegistry.UnexpectedExceptionErrorCode));
            if (!expected)
                LogAssert.Expect(LogType.Exception,
                    new Regex("InvalidOperationException: CLI exception evidence fixture"));
            var step = VmProjectToolRegistry.ExecuteJobStepInline(
                VmProjectToolExceptionEvidenceJobFixture.ToolName,
                new Dictionary<string, object> { { "expected", expected } },
                new Dictionary<string, object>());
            Assert.That(step.IsComplete, Is.True);
            var result = (Dictionary<string, object>)step.Result;
            Assert.That(result["errorCode"],
                Is.EqualTo(expected ? "expected_fixture_rejection" : "project_tool_exception"));
            Assert.That(result["toolName"], Is.EqualTo(VmProjectToolExceptionEvidenceJobFixture.ToolName));
            Assert.That(result.ContainsKey("exceptionType"), Is.EqualTo(!expected));
            if (!expected)
                Assert.That(result["stackTrace"].ToString(), Does.Contain("ExecuteJobStep"));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
