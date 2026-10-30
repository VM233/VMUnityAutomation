using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace VMUnityAutomation.Editor
{
    internal sealed class CloudServiceCheckWork
    {
        internal string JobID { get; }
        internal string Token { get; }
        internal Task<Dictionary<string, object>> Source { get; private set; }
        internal Task<VmProjectToolCleanupReceipt> Retirement => retirement.Task;
        private readonly CancellationTokenSource cancellation = new();
        private readonly TaskCompletionSource<VmProjectToolCleanupReceipt> retirement = new();
        private readonly object lifetime = new();

        internal CloudServiceCheckWork(string job, string token) { JobID = job; Token = token; }

        internal void Start(Func<CancellationToken, Task<string>> request)
        {
            if (Source != null) throw new InvalidOperationException("The native cloud request was already started.");
            cancellation.CancelAfter(15000);
            Source = Run(request);
        }

        internal void Cancel()
        {
            lock (lifetime) if (!Retirement.IsCompleted) cancellation.Cancel();
        }

        private async Task<Dictionary<string, object>> Run(Func<CancellationToken, Task<string>> request)
        {
            var result = new Dictionary<string, object>
            {
                ["protocol"] = "unity-native-service-token-check-v1", ["checkedAt"] = DateTime.UtcNow.ToString("O"),
                ["outcome"] = "empty", ["tokenAvailable"] = false,
                ["cacheSemantics"] = "Unity-owned cache; no forced refresh and no claim of a fresh exchange.", ["failure"] = null
            };
            try
            {
                string token = await request(cancellation.Token);
                result["tokenAvailable"] = !string.IsNullOrEmpty(token);
                result["outcome"] = string.IsNullOrEmpty(token) ? "empty" : "available";
                token = null;
            }
            catch (OperationCanceledException) { result["outcome"] = "cancelled"; }
            catch (Exception exception) { result["outcome"] = "failed"; result["failure"] = DescribeFailure(exception); }
            finally
            {
                lock (lifetime)
                {
                    cancellation.Dispose();
                    retirement.SetResult(new VmProjectToolCleanupReceipt(JobID, Token, true,
                        new Dictionary<string, object> { ["resourcesReleased"] = true }));
                }
            }
            return result;
        }

        internal static Dictionary<string, object> DescribeFailure(Exception exception)
        {
            object Read(string name) => exception.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(exception);
            string raw = Read("url") as string;
            string target = null;
            if (raw != null && raw.Length <= 2048 && Uri.TryCreate(raw, UriKind.Absolute, out Uri uri) &&
                (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
                target = new UriBuilder(uri.Scheme, uri.Host, uri.Port, uri.AbsolutePath).Uri.GetLeftPart(UriPartial.Path);
            string method = Read("method") as string;
            return new Dictionary<string, object>
            {
                ["exceptionType"] = exception.GetType().FullName,
                ["responseCode"] = Read("responseCode") is long code ? code : null,
                ["isHttpError"] = Read("isHttpError") is bool http ? http : null,
                ["isNetworkError"] = Read("isNetworkError") is bool network ? network : null,
                ["method"] = method == "GET" || method == "POST" || method == "HEAD" ? method : null,
                ["target"] = target,
                ["timeout"] = Read("timeout") is int timeout ? timeout : null
            };
        }
    }
}
