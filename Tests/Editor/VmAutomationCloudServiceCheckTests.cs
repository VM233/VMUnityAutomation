using System;
using System.Collections;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmAutomationCloudServiceCheckTests
    {
        [Test]
        public void AvailabilityRetiresWithoutPublishingTheToken()
        {
            var work = NewWork();
            int calls = 0;
            work.Start(_ => { calls++; return Task.FromResult("private-service-token"); });
            var result = work.Source.GetAwaiter().GetResult();
            Assert.That(result["outcome"], Is.EqualTo("available"));
            Assert.That(result["tokenAvailable"], Is.True);
            Assert.That(JsonConvert.SerializeObject(result), Does.Not.Contain("private-service-token"));
            Assert.That(work.Retirement.Result.ResourcesReleased, Is.True);
            Assert.Throws<InvalidOperationException>(() => work.Start(_ => { calls++; return Task.FromResult(""); }));
            Assert.That(calls, Is.EqualTo(1));
            work.Cancel();
        }

        [Test]
        public void EmptyNativeTokenIsNotAvailability()
        {
            var work = NewWork();
            work.Start(_ => Task.FromResult<string>(null));
            Assert.That(work.Source.Result["outcome"], Is.EqualTo("empty"));
            Assert.That(work.Source.Result["tokenAvailable"], Is.False);
            Assert.That(work.Retirement.Result.ResourcesReleased, Is.True);
        }

        [Test]
        public void ActualFailureMetadataDropsCredentialsBodiesAndHeaders()
        {
            var work = NewWork();
            work.Start(_ => Task.FromException<string>(new NativeRequestFailure()));
            var result = work.Source.Result;
            string text = JsonConvert.SerializeObject(result);
            Assert.That(result["outcome"], Is.EqualTo("failed"));
            Assert.That(text, Does.Contain("401"));
            Assert.That(text, Does.Contain("https://services.unity.com/api/auth/exchange"));
            Assert.That(text, Does.Not.Contain("private-"));
            Assert.That(text, Does.Not.Contain("responseHeaders"));
            Assert.That(work.Retirement.Result.ResourcesReleased, Is.True);
        }

        [Test]
        public void UnknownExceptionAndOversizedUrlRemainRedacted()
        {
            var plain = CloudServiceCheckWork.DescribeFailure(new Exception("private-message"));
            Assert.That(plain["responseCode"], Is.Null);
            Assert.That(plain["target"], Is.Null);
            Assert.That(JsonConvert.SerializeObject(plain), Does.Not.Contain("private-message"));
            var huge = new NativeRequestFailure { url = "https://services.unity.com/" + new string('x', 2048) };
            Assert.That(CloudServiceCheckWork.DescribeFailure(huge)["target"], Is.Null);
        }

        [UnityTest]
        public IEnumerator CancellationWaitsForTheSameActualRetirement()
        {
            var work = NewWork();
            var source = new TaskCompletionSource<string>();
            work.Start(token => { token.Register(() => source.TrySetCanceled()); return source.Task; });
            Task<VmProjectToolCleanupReceipt> actual = work.Retirement;
            Assert.That(actual.IsCompleted, Is.False);
            work.Cancel();
            for (int frame = 0; frame < 10 && !actual.IsCompleted; frame++) yield return null;
            Assert.That(work.Retirement, Is.SameAs(actual));
            Assert.That(actual.IsCompleted, Is.True);
            Assert.That(actual.Result.ResourcesReleased, Is.True);
            Assert.That(work.Source.Result["outcome"], Is.EqualTo("cancelled"));
            work.Cancel();
        }

        private static CloudServiceCheckWork NewWork() => new(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));

        private sealed class NativeRequestFailure : Exception
        {
            public string url { get; set; } = "https://private-user:private-password@services.unity.com/api/auth/exchange?token=private-query#private-fragment";
            public long responseCode => 401;
            public bool isHttpError => true;
            public bool isNetworkError => false;
            public string method => "POST";
            public int timeout => 15;
            public string responseHeaders => "private-cookie";
        }
    }
}
