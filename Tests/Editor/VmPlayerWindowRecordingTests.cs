using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmPlayerWindowRecordingTests
    {
        [Test]
        public void CompleteGesturesRejectPartialOrUnrelatedDestinationsBeforeNativeInput()
        {
            Assert.Throws<VmProjectToolException>(() => VmPlayerPointerTool.ValidateGesture(
                new VmPlayerPointerRequest { Action = VmPlayerPointerAction.Drag }));
            Assert.Throws<VmProjectToolException>(() => VmPlayerPointerTool.ValidateGesture(
                new VmPlayerPointerRequest { Action = VmPlayerPointerAction.Drag, EndX = 1 }));
            Assert.Throws<VmProjectToolException>(() => VmPlayerPointerTool.ValidateGesture(
                new VmPlayerPointerRequest { Action = VmPlayerPointerAction.Click, EndY = 1 }));
            Assert.DoesNotThrow(() => VmPlayerPointerTool.ValidateGesture(
                new VmPlayerPointerRequest { Action = VmPlayerPointerAction.Drag, EndX = -1920, EndY = 500 }));
            var type = typeof(VmPlayerPointerTool);
            var attribute = (VmProjectToolAttribute)System.Attribute.GetCustomAttribute(type, typeof(VmProjectToolAttribute));
            Assert.That(VmProjectToolDescriptor.FromType(attribute, type).ValidationError, Is.Null.Or.Empty);
        }

        [TestCase(0, 0, 5120)]
        [TestCase(5119, 0, 5120)]
        [TestCase(-1920, -1920, 7040)]
        [TestCase(-1, -1920, 7040)]
        [TestCase(5119, -1920, 7040)]
        [TestCase(7, 7, 1)]
        [TestCase(65535, 0, 65536)]
        [TestCase(int.MinValue, int.MinValue, 1600)]
        public void AbsoluteMotionTargetsTheRequestedVirtualDesktopPixel(int pixel, int origin, int size)
        {
            int normalized = VmPlayerPointerTool.NormalizeAbsoluteCoordinate(pixel, origin, size);
            Assert.That(normalized, Is.InRange(0, 65535));
            Assert.That((long)normalized * size / 65536 + origin, Is.EqualTo(pixel));
        }

        [TestCase(-1, 0, 5120)]
        [TestCase(5120, 0, 5120)]
        [TestCase(0, 0, 0)]
        [TestCase(0, 0, 65537)]
        [TestCase(int.MaxValue, int.MinValue, 1600)]
        public void AbsoluteMotionRejectsUnaddressablePixels(int pixel, int origin, int size)
        {
            var error = Assert.Throws<VmProjectToolException>(() =>
                VmPlayerPointerTool.NormalizeAbsoluteCoordinate(pixel, origin, size));
            Assert.That(error.ErrorCode, Is.EqualTo("invalid_player_pointer_arguments"));
        }

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
