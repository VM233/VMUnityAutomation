using System;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmAutomationExceptionEvidenceTests
    {
        [TestCase("command_exception")]
        [TestCase("workspace_job_execution_failed")]
        [TestCase("job_execution_failed")]
        [TestCase("job_cleanup_failed")]
        public void UnexpectedFailureRetainsCauseStackAndOuterContext(string code)
        {
            Exception caught;
            try
            {
                throw new FormatException("Native metadata rejection.");
            }
            catch (FormatException cause)
            {
                caught = new InvalidOperationException("Import stage.", cause);
            }

            var result = VmAutomationResponse.UnexpectedError(caught, code);
            Assert.That(result["success"], Is.False);
            Assert.That(result["errorCode"], Is.EqualTo(code));
            Assert.That(result["exceptionType"], Is.EqualTo(typeof(FormatException).FullName));
            Assert.That(result["stackTrace"].ToString(), Does.Contain(nameof(UnexpectedFailureRetainsCauseStackAndOuterContext)));
            Assert.That(result["exceptionContext"].ToString(), Does.Contain("Import stage."));
            Assert.That(result["exceptionContext"].ToString(), Does.Contain("Native metadata rejection."));
            Assert.That(VmAutomationResponse.NormalizeError(result)["exceptionContext"],
                Is.EqualTo(result["exceptionContext"]));
        }
    }
}
