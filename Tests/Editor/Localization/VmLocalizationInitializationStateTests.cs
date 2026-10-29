using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace VMUnityAutomation.Editor.Localization.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmLocalizationInitializationStateTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private LocalizationSettings settings;
        private ResourceManager manager;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            manager = new ResourceManager();
        }

        [TearDown]
        public void TearDown()
        {
            typeof(LocalizationSettings).GetField("m_InitializingOperationHandle", Flags)
                .SetValue(settings, default(AsyncOperationHandle<LocalizationSettings>));
            UnityEngine.Object.DestroyImmediate(settings);
            manager.Dispose();
        }

        [Test]
        public void OptionalAssemblyDeclaresItsActualPackageOwner()
        {
            var owner = typeof(VmLocalizationInitializationStateTool).Assembly
                .GetCustomAttribute<VmProjectToolPackageAttribute>();
            Assert.That(owner.PackageId, Is.EqualTo("com.vm233.unity-automation"));
        }

        [Test]
        public void AbsentSettingsAreExplicit()
        {
            var result = Capture(null);
            Assert.That(result["hasActiveSettings"], Is.False);
            Assert.That(result["root"], Is.Null);
        }

        [Test]
        public void QueryDoesNotStartAnUnstartedHandle()
        {
            Assert.That(Root()["handleState"], Is.EqualTo("None"));
            Assert.That(Cached(), Is.EqualTo(default(AsyncOperationHandle<LocalizationSettings>)));
        }

        [Test]
        public void ReleasedHandleRemainsInvalidWithoutReplacement()
        {
            var handle = manager.CreateCompletedOperation(settings, null);
            manager.Release(handle);
            manager.Update(0);
            SetCached(handle);
            Assert.That(Root()["handleState"], Is.EqualTo("Invalid"));
            Assert.That(Cached(), Is.EqualTo(handle));
            Assert.That(handle.IsValid(), Is.False);
        }

        [Test]
        public void FailureIsNotChangedToSuccessOrReleased()
        {
            var originalHandler = ResourceManager.ExceptionHandler;
            AsyncOperationHandle<LocalizationSettings> handle = default;
            try
            {
                ResourceManager.ExceptionHandler = (operation, exception) => { };
                handle = manager.CreateCompletedOperation<LocalizationSettings>(null, "expected fixture failure");
                SetCached(handle);
                int references = ReferenceCount(handle);
                var root = Root();
                Assert.That(root["status"], Is.EqualTo("Failed"));
                Assert.That(root["isDone"], Is.True);
                Assert.That(root["exceptionType"], Is.EqualTo(typeof(Exception).FullName));
                Assert.That(ReferenceCount(handle), Is.EqualTo(references));
                Assert.That(Cached(), Is.EqualTo(handle));
            }
            finally
            {
                ResourceManager.ExceptionHandler = originalHandler;
                if (handle.IsValid())
                    manager.Release(handle);
            }
        }

        [Test]
        public void PendingObservationDoesNotExecuteCompleteOrAcquire()
        {
            var operation = new PendingOperation();
            var handle = manager.StartOperation(operation, default(AsyncOperationHandle));
            SetCached(handle);
            try
            {
                int executions = operation.Executions;
                int references = ReferenceCount(handle);
                var root = Root();
                Assert.That(root["status"], Is.EqualTo("None"));
                Assert.That(root["isDone"], Is.False);
                Assert.That(operation.Executions, Is.EqualTo(executions));
                Assert.That(ReferenceCount(handle), Is.EqualTo(references));
                Assert.That(Cached(), Is.EqualTo(handle));
            }
            finally
            {
                operation.Finish(settings);
                manager.Release(handle);
            }
        }

        private Dictionary<string, object> Root()
        {
            return (Dictionary<string, object>)Capture(settings)["root"];
        }

        private static Dictionary<string, object> Capture(LocalizationSettings target)
        {
            var method = typeof(VmLocalizationInitializationStateTool).GetMethod("Capture",
                BindingFlags.Static | BindingFlags.NonPublic);
            return (Dictionary<string, object>)method.Invoke(null, new object[] { target });
        }

        private void SetCached(AsyncOperationHandle<LocalizationSettings> handle)
        {
            typeof(LocalizationSettings).GetField("m_InitializingOperationHandle", Flags).SetValue(settings, handle);
        }

        private AsyncOperationHandle<LocalizationSettings> Cached()
        {
            return (AsyncOperationHandle<LocalizationSettings>)typeof(LocalizationSettings)
                .GetField("m_InitializingOperationHandle", Flags).GetValue(settings);
        }

        private static int ReferenceCount(AsyncOperationHandle<LocalizationSettings> handle)
        {
            return (int)typeof(AsyncOperationHandle<LocalizationSettings>)
                .GetProperty("ReferenceCount", Flags).GetValue(handle);
        }

        private sealed class PendingOperation : AsyncOperationBase<LocalizationSettings>
        {
            public int Executions { get; private set; }

            protected override void Execute()
            {
                Executions++;
            }

            public void Finish(LocalizationSettings value)
            {
                Complete(value, true, (string)null);
            }
        }
    }
}
