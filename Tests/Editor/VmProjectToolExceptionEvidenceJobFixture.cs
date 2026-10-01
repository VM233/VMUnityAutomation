using System.Collections.Generic;

namespace VMUnityAutomation.Editor.Tests
{
    [VmProjectTool(ToolName, ReadOnly = true, LongRunning = true,
        Description = "Test fixture for cooperative project-tool exception evidence.",
        InputSchemaJson = VmProjectToolExceptionEvidenceTests.InputSchema,
        OutputSchemaJson = VmProjectToolExceptionEvidenceTests.OutputSchema,
        ErrorCodes = new[] { "expected_fixture_rejection" },
        SideEffects = VmProjectToolSideEffect.ReadsProjectState)]
    public sealed class VmProjectToolExceptionEvidenceJobFixture : IVmPersistentProjectTool
    {
        public const string ToolName = "tests/cli-job-exception-evidence";

        public object Execute(Dictionary<string, object> arguments)
            => VmProjectToolExceptionEvidenceTests.Fixture(arguments);

        public VmProjectToolJobStep ExecuteJobStep(
            Dictionary<string, object> arguments, Dictionary<string, object> state)
            => VmProjectToolJobStep.Complete(Execute(arguments));
    }
}
