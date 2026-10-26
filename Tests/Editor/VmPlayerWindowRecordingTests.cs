using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmPlayerWindowRecordingTests
    {
        [Test]
        public void NativeRecorderPublishesAValidTypedProjectToolContract()
        {
            var type = typeof(VmPlayerWindowRecordingTool);
            var attribute = (VmProjectToolAttribute)System.Attribute.GetCustomAttribute(type, typeof(VmProjectToolAttribute));
            var descriptor = VmProjectToolDescriptor.FromType(attribute, type);
            Assert.That(descriptor.ValidationError, Is.Null.Or.Empty);
            Assert.That(descriptor.MutatesProjectFiles, Is.True);
            Assert.That(descriptor.InputSchema, Is.Not.Null);
            Assert.That(descriptor.OutputSchema, Is.Not.Null);
        }

        [TestCase(0, 2, 1, 1, 0)]
        [TestCase(4096, 4096, 1, 1, 0)]
        [TestCase(2, 2, double.NaN, 1, 0)]
        [TestCase(2, 2, 31, 1, 0)]
        [TestCase(2, 2, 1, 61, 0)]
        [TestCase(2, 2, 1, 1, -1)]
        public void OutOfBudgetAdmissionRejectsBeforeProcessOrFileAccess(
            int width, int height, double seconds, int fps, int x)
        {
            var tool = new VmPlayerWindowRecordingTool();
            var error = Assert.Throws<VmProjectToolException>(() => tool.Execute(new VmPlayerWindowRecordingRequest
            {
                Width = width, Height = height, DurationSeconds = seconds,
                FramesPerSecond = fps, X = x
            }));
#if UNITY_EDITOR_WIN
            StringAssert.Contains("bounded domains", error.Message);
#else
            StringAssert.Contains("requires Windows", error.Message);
#endif
        }
    }
}
