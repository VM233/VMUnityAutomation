using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmPlayerTerminateTests
    {
#if UNITY_EDITOR_WIN
        [Test]
        public void MissingLaunchIdentityCannotReachNativeTermination()
        {
            var error = Assert.Throws<VmProjectToolException>(() =>
                new VmPlayerTerminateTool().Execute(new VmPlayerIdentityRequest()));
            Assert.That(error.ErrorCode, Is.EqualTo("invalid_player_identity_arguments"));
        }
#endif
        [Test]
        public void TerminationIsExplicitlyDangerousAndDistinctFromNormalQuit()
        {
            var contract = (VmProjectToolAttribute)System.Attribute.GetCustomAttribute(
                typeof(VmPlayerTerminateTool), typeof(VmProjectToolAttribute));
            Assert.That(contract.Dangerous, Is.True);
            Assert.That(contract.ToolName, Is.EqualTo("player/terminate"));
            Assert.That(contract.LongRunning, Is.False);
        }
    }
}
