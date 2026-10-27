using System;
using System.Reflection;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmRuntimeKeyboardAdmissionTests
    {
        private static MethodInfo Parser()
        {
            var type = Type.GetType("VMUnityAutomation.Editor.InputSystem.VmRuntimeKeyboardTool, VMUnityAutomation.Editor.InputSystem");
            if (type == null)
                Assert.Ignore("The optional Unity 6 Input System integration is not installed.");
            return type.GetMethod("ParseKeys", BindingFlags.Static | BindingFlags.NonPublic);
        }

        private static object[] InvalidChords => new object[]
        {
            new object[] { null }, new object[] { Array.Empty<string>() },
            new object[] { new[] { "A", "B", "C", "D", "E" } },
            new object[] { new[] { "LeftAlt", "leftalt" } },
            new object[] { new[] { "None" } }, new object[] { new[] { "1" } },
            new object[] { new[] { "NotAKey" } }
        };

        [TestCaseSource(nameof(InvalidChords))]
        public void RejectsInvalidChordBeforeAnyNativeMutation(string[] keys)
        {
            var exception = Assert.Throws<TargetInvocationException>(() => Parser().Invoke(null, new object[] { keys }));
            Assert.That(exception.InnerException, Is.TypeOf<VmProjectToolException>());
        }

        [Test]
        public void AcceptsNamedChordWithoutExpandingItIntoExtraInput()
        {
            var keys = (Array)Parser().Invoke(null, new object[] { new[] { "LeftCtrl", "LeftShift", "A" } });
            Assert.That(keys.Length, Is.EqualTo(3));
            Assert.That(keys.GetValue(0).ToString(), Is.EqualTo("LeftCtrl"));
            Assert.That(keys.GetValue(1).ToString(), Is.EqualTo("LeftShift"));
            Assert.That(keys.GetValue(2).ToString(), Is.EqualTo("A"));
        }
    }
}
