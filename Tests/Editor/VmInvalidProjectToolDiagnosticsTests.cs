using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using VMUnityAutomation.Editor;

namespace VMUnityAutomation.Editor.Tests
{
    internal sealed class VmInvalidProjectToolDiagnosticsTests
    {
        private const string InvalidToolName =
            "tests/invalid-project-tool-diagnostics";
        private const string EmptyObjectSchema =
            "{\"type\":\"object\",\"properties\":{}," +
            "\"additionalProperties\":false}";

        [VmProjectTool(InvalidToolName,
            Description = "Intentionally invalid test fixture.",
            InputSchemaJson = EmptyObjectSchema,
            OutputSchemaJson = EmptyObjectSchema,
            SideEffects = VmProjectToolSideEffect.ReadsProjectState)]
        public static object InvalidFixture(
            Dictionary<string, object> arguments)
        {
            return new Dictionary<string, object>();
        }

        [Test]
        public async Task ExecuteAsync_InvalidProjectTool_ReturnsValidationError()
        {
            VmProjectToolRegistry.ResetCacheForTests();
            string generatedCommand =
                VmAutomationCatalog.ProjectToolNameToToolName(
                    InvalidToolName);

            VmAutomationInvocationResult result =
                await VmAutomationExecutor.ExecuteAsync(
                    generatedCommand,
                    new Dictionary<string, object>(),
                    requestId: "invalid-project-tool-diagnostics-test");

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error.Code,
                Is.EqualTo("invalid_project_tool"));
            Assert.That(result.Error.Message,
                Does.Contain("discovered"));
            Assert.That(result.Error.Message,
                Does.Contain("must explicitly declare"));
            Assert.That(result.Error.Details["projectToolName"],
                Is.EqualTo(InvalidToolName));
            Assert.That(result.Error.Details["generatedCommand"],
                Is.EqualTo(generatedCommand));
            Assert.That(result.Error.Details["validationError"].ToString(),
                Does.Contain("ReadOnly"));
        }
    }
}
